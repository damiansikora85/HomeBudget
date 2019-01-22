﻿//using Acr.UserDialogs;
using Acr.UserDialogs;
using HomeBudgeStandard.Interfaces;
using HomeBudget.Code;
using HomeBudget.Code.Logic;
using HomeBudget.Converters;
using HomeBudget.Utils;
using Syncfusion.Data;
using Syncfusion.SfDataGrid.XForms;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace HomeBudgeStandard.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class BudgetPlanView : ContentPage, IActiveAware
    {
        public ObservableCollection<BudgetViewModelData> Budget { get; set; }

        private bool _setupDone;
        private bool _hasIncomes;
        private bool _hasExpenses;
        private DateTime _currentMonth;
        private SfDataGrid _dataGrid;

        private Label _expensesChartSwitch;
        private Label _incomeChartSwitch;
        private Grid _mainGrid;
        private BudgetChart _chartExpense;
        private BudgetChart _chartIncome;
        private Button _previousMonthButton;
        private Button _nextMonthButton;

        private CultureInfo _cultureInfoPL = new CultureInfo("pl-PL");

        public string Date
        {
            get => _currentMonth.ToString("MMMM yyyy", _cultureInfoPL);
        }

        public event EventHandler IsActiveChanged;

        bool _isActive;
        public virtual bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                }
            }
        }

        public BudgetPlanView ()
		{
            _currentMonth = DateTime.Now;
            Budget = new ObservableCollection<BudgetViewModelData>();
            BindingContext = this;
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            if (!IsActive) return;

            if (MainBudget.Instance.IsDataLoaded && !_setupDone)
            {
                Task.Run(async () => await Setup());
            }
            else
            {
                UpdateCharts(_currentMonth);
                ForceSwitchChart(_expensesChartSwitch);
            }

            _setupDone = true;
        }

        public async Task Activate()
        {
            UserDialogs.Instance.ShowLoading("");

            var dataTemplate = (DataTemplate)Resources["ContentTemplate"];
            View view = null;
            await Task.Factory.StartNew(() =>
            {
                view = (View)dataTemplate.CreateContent();
            });
            this.Content = view;

            SetupVariables();
            CreateDataGrid();

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += SwitchChart;
            _expensesChartSwitch.GestureRecognizers.Add(tapGesture);
            _incomeChartSwitch.GestureRecognizers.Add(tapGesture);

            if (MainBudget.Instance.IsDataLoaded && !_setupDone)
            {
                Setup();
            }
            else
            {
                UpdateCharts(_currentMonth);
                ForceSwitchChart(_expensesChartSwitch);
            }

            _setupDone = true;

            UserDialogs.Instance.HideLoading();
        }

        private void SetupVariables()
        {
            _expensesChartSwitch = this.Content.FindByName<Label>("ExpensesChartSwitch");
            _incomeChartSwitch = this.Content.FindByName<Label>("IncomeChartSwitch");
            _mainGrid = this.Content.FindByName<Grid>("mainGrid");
            _chartExpense = this.Content.FindByName<BudgetChart>("chartExpense");
            _chartIncome = this.Content.FindByName<BudgetChart>("chartIncome");
            _previousMonthButton = this.Content.FindByName<Button>("PreviousMonthButton");
            _nextMonthButton = this.Content.FindByName<Button>("NextMonthButton");
        }

        private async Task Setup()
        {
            _currentMonth = DateTime.Now;

            await SetupDataGrid(_currentMonth);
            UpdateCharts(_currentMonth);
            ForceSwitchChart(_expensesChartSwitch);
        }

        private void CreateDataGrid()
        {
            _dataGrid = new SfDataGrid()
            {
                EnableDataVirtualization = true,
                AutoGenerateColumns = false,
                AutoExpandGroups = false,
                AllowGroupExpandCollapse = true,
                LiveDataUpdateMode = LiveDataUpdateMode.AllowSummaryUpdate,
                SelectionMode = SelectionMode.Single,
                NavigationMode = NavigationMode.Cell,
                EditTapAction = TapAction.OnTap
            };

            _dataGrid.SortComparers.Add(new SortComparer
            {
                PropertyName = "Category.Name",
                Comparer = new BudgetCategorySortComparer()
            });

            _dataGrid.CurrentCellEndEdit += DataGrid_CurrentCellEndEdit;
            _dataGrid.GroupColumnDescriptions.Add(new GroupColumnDescription
            {
                ColumnName = "Category.Name"
            });

            var gridSummaryRow = new GridGroupSummaryRow
            {
                ShowSummaryInRow = true,
                Title = "{Key}: {Total}",
                SummaryColumns = new ObservableCollection<ISummaryColumn>
                {
                    new GridSummaryColumn
                    {
                        Name = "Total",
                        MappingName="Subcat.Value",
                        SummaryType= SummaryType.Custom,
                        CustomAggregate = new CurrencyDataGridHeader(),
                        Format = "{Currency}"
                    }
                }
            };
            _dataGrid.CaptionSummaryRow = gridSummaryRow;

            _dataGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "Subcat.Name",
                HeaderText = "Kategoria",
                HeaderFontAttribute = FontAttributes.Bold,
                Width = 100
            });

            _dataGrid.Columns.Add(new GridNumericColumn
            {
                MappingName = "SubcatPlanned.Value",
                HeaderText = "Suma",
                AllowEditing = true,
                HeaderFontAttribute = FontAttributes.Bold,
                ColumnSizer = ColumnSizer.Star,
                DisplayBinding = new Binding() { Path = "SubcatPlanned.Value", Converter = new CurrencyValueConverter() }
        });

            _dataGrid.SetBinding(SfDataGrid.ItemsSourceProperty, nameof(Budget));

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

            grid.Children.Add(_dataGrid);
            var button = new Button { Text = "Użyj w kolejnych miesiącach", BackgroundColor = Color.DodgerBlue, TextColor = Color.White, VerticalOptions = LayoutOptions.End, Margin= new Thickness(12, 12) };
            button.Clicked += OnSave;
            grid.Children.Add(button);
            Grid.SetColumn(button, 1);

            Grid.SetRow(grid, 2);
            _mainGrid.Children.Add(grid);
        }

        private void UpdateCharts(DateTime date)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                var budgetPlanned = MainBudget.Instance.GetMonth(date).BudgetPlanned;
                var chartDataExpenses = new List<ChartData>();
                foreach (BudgetPlannedCategory category in budgetPlanned.Categories)
                {
                    if (!category.IsIncome && category.TotalValues > 0)
                    {
                        chartDataExpenses.Add(new ChartData { Label = category.Name, Value = category.TotalValues });
                    }
                }
                _chartExpense.SetData(chartDataExpenses);


                var chartDataIncome = new List<ChartData>();
                var incomesCategories = budgetPlanned.GetIncomesCategories();
                foreach (BudgetPlannedCategory category in incomesCategories)
                {
                    foreach (BaseBudgetSubcat subcat in category.subcats)
                    {
                        if (subcat.Value > 0)
                            chartDataIncome.Add(new ChartData { Label = subcat.Name, Value = subcat.Value });
                    }
                }

                _chartIncome.SetData(chartDataIncome);
            });
        }

        private async Task SetupDataGrid(DateTime date)
        {
            await Task.Run(() =>
            {
                var budget = new ObservableCollection<BudgetViewModelData>();
                try
                {
                    var budgetPlanned = MainBudget.Instance.GetMonth(date).BudgetPlanned;

                    foreach (var category in budgetPlanned.Categories)
                    {
                        foreach (var subcat in category.subcats)
                        {
                            var model = new BudgetViewModelData
                            {
                                Category = category,
                                Subcat = subcat,
                                SubcatPlanned = subcat as PlannedSubcat,
                            };
                            budget.Add(model);
                        }
                    }
                }
                catch (Exception e)
                {
                    var msg = e.Message;
                    return;
                }

                Device.BeginInvokeOnMainThread(() =>
                {
                    Budget = budget;
                    OnPropertyChanged(nameof(Budget));

                    _previousMonthButton.IsEnabled = MainBudget.Instance.HasMonthData(_currentMonth.AddMonths(-1));
                    _nextMonthButton.IsEnabled = MainBudget.Instance.HasMonthData(_currentMonth.AddMonths(1));
                });
            });
        }

        private void ForceSwitchChart(Label label)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                label.TextDecorations = TextDecorations.Underline;
                if (label == _expensesChartSwitch)
                {
                    _incomeChartSwitch.TextDecorations = TextDecorations.None;
                    _chartExpense.IsVisible = true;
                    _chartIncome.IsVisible = false;
                }
                else
                {
                    _expensesChartSwitch.TextDecorations = TextDecorations.None;
                    _chartExpense.IsVisible = false;
                    _chartIncome.IsVisible = true;
                }
            });
        }

        private void SwitchChart(object sender, EventArgs e)
        {
            if (sender is View view && view.Effects.Count == 0)
                ForceSwitchChart(sender as Label);
        }

        private async void OnSave(object sender, EventArgs e)
        {
            await MainBudget.Instance.UpdateMainPlannedBudget();
            UserDialogs.Instance.Toast("Ten plan budżetu będzie używany w kolejnych miesiącach");
        }

        private void DataGrid_CurrentCellEndEdit(object sender, GridCurrentCellEndEditEventArgs e)
        {
            Task.Run(() => MainBudget.Instance.Save());

            Device.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(100);

                _dataGrid.View.TopLevelGroup.UpdateCaptionSummaries();
                _dataGrid.View.Refresh();

                UpdateCharts(_currentMonth);   
            });
        }

        private async void OnPrevMonth(object sender, EventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(-1);
            await RefreshAsync();
        }

        private async void OnNextMonth(object sender, EventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(1);
            await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            OnPropertyChanged(nameof(Date));
            await SetupDataGrid(_currentMonth);
            UpdateCharts(_currentMonth);
        }
    }
}
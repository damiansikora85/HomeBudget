﻿using HomeBudget.Code.Logic;
using HomeBudgetShared.Code.Interfaces;
using HomeBudgetShared.Code.Synchronize;
using HomeBudgetShared.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Threading.Tasks;

namespace HomeBudget.Code
{
    public struct CategoryData
    {
        public string Name;
        public int Id;
        public CategoryData(string name, int id)
        {
            Name = name;
            Id = id;
        }
    }

    public class GroupedCategory : ObservableCollection<SimpleCategory>
    {
        public string Name { get; set; }
        public double Total { get; set; }
        public int Id;
    }

    public class SimpleCategory
    {
        public string Name { get; set; }
        public double Value { get; set; }
        public int Id;
    }

    public class MainBudget
	{
        private const string TEMPLATE_FILENAME = "template.json";
        private const string SAVE_DIRECTORY_NAME = "save";
        private const string SAVE_FILE_NAME = "budget.data";
        public const int INCOME_CATEGORY_ID = 777;
        private const int VERSION = 1;
        private List<BudgetMonth> _months;
        private BudgetPlanned budgetPlanned;

        private object _updateLock = new object();

        private IFileManager _fileManager;
        private IBudgetSynchronizer _budgetSynchronizer;
        public bool IsDataLoaded { get; private set; }

        public event Action onPlannedBudgetChanged;
        public event Action<bool> BudgetDataChanged = delegate { };

        public BudgetDescription BudgetDescription { get; private set; }

        public BudgetData ActualBudgetData { get; private set; }

        static MainBudget instance;
        public static MainBudget Instance
        {
            get
            {
                if (instance == null)
                    instance = new MainBudget();

                return instance;
            }
        }

        private MainBudget()
        {
            _months = new List<BudgetMonth>();
            budgetPlanned = new BudgetPlanned();
            IsDataLoaded = false;
        }

        public void OnCloudStorageConnected(bool overwriteLocal)
        {
            IsDataLoaded = !overwriteLocal;
            _budgetSynchronizer.ShouldSave = !overwriteLocal;
            _budgetSynchronizer.Start();

            if (overwriteLocal)
                Task.Run(async () => UpdateData(null, await _budgetSynchronizer.ForceLoad()));
        }

        public void Init(IFileManager fileManager, IBudgetSynchronizer budgetSynchronizer)
        {
            LogsManager.Instance.Init(fileManager);

            LogsManager.Instance.WriteLine("***********************************");
            LogsManager.Instance.WriteLine("MainBudget - Init");

            _fileManager = fileManager;
            _budgetSynchronizer = budgetSynchronizer;
            _budgetSynchronizer.DataDownloaded += UpdateData;

            Task.Factory.StartNew(() =>
            {
                try
                {
                    var assembly = typeof(MainBudget).GetTypeInfo().Assembly;
                    //var name = assembly.GetName();
                    //var names = assembly.GetManifestResourceNames();
                    var stream = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.{TEMPLATE_FILENAME}");
                    var jsonString = "";
                    using (var reader = new System.IO.StreamReader(stream))
                    {
                        jsonString = reader.ReadToEnd();
                        BudgetDescription = JsonConvert.DeserializeObject<BudgetDescription>(jsonString);
                        budgetPlanned.Setup(BudgetDescription.Categories);
                    }

                    if (!string.IsNullOrEmpty(Helpers.Settings.DropboxAccessToken))
                    {
                        _budgetSynchronizer.Start();
                        LogsManager.Instance.WriteLine("Load data from cloud storage");
                        Task.Run(async () => UpdateData(null, await _budgetSynchronizer.ForceLoad()));
                    }
                    else
                    {
                        LogsManager.Instance.WriteLine("Load data from local device");
                        Task.Run(() => LoadAsync());
                    }
                }
                catch (Exception exc)
                {
                    var msg = exc.Message;
                    LogsManager.Instance.WriteLine(exc.Message);
                    LogsManager.Instance.WriteLine(exc.StackTrace);
                }
            });

            
        }

        public async Task<bool> Save(bool upload = true)
        {
            try
            {
                LogsManager.Instance.WriteLine("Save data");
                ActualBudgetData = new BudgetData
                {
                    Version = VERSION,
                    TimeStamp = DateTime.Now,
                    BudgetPlanned = budgetPlanned,
                    Months = _months,
                    IsSynchronized = false
                };
                await _fileManager.Save(ActualBudgetData);

                if (upload)
                    _budgetSynchronizer.ShouldSave = true;

            }
            catch (Exception exc)
            {
                LogsManager.Instance.WriteLine("Save data error: " + exc.InnerException != null ? exc.InnerException.Message : string.Empty);
                LogsManager.Instance.WriteLine(exc.Message);
                LogsManager.Instance.WriteLine(exc.StackTrace);
            }

            return true;
        }

        public async Task LoadAsync()
        {
            try
            {
                LogsManager.Instance.WriteLine("Load data");
                var data = await _fileManager.Load();
                if (data != null)
                {
                    budgetPlanned = data.BudgetPlanned;
                    _months = data.Months;
                    foreach (var month in _months)
                        month.Setup();
                }

                IsDataLoaded = true;
                //onBudgetLoaded?.Invoke();
                BudgetDataChanged?.Invoke(false);
            }
            catch (Exception exc)
            {
                LogsManager.Instance.WriteLine(exc.Message);
                LogsManager.Instance.WriteLine(exc.StackTrace);
            }
        }

        private void OnPlannedBudgetChanged()
        {
            onPlannedBudgetChanged?.Invoke();
        }

        private void UpdateData(object sender, BudgetData data)
        {
            try
            {
                if (data == null)
                {
                    LogsManager.Instance.WriteLine("Cloud UpdateData == null");
                    Task.Run(() => LoadAsync());
                }
                else
                {
                    lock (_updateLock)
                    {
                        _months.Clear();
                        if (data.Months != null)
                        {
                            _months = data.Months;
                            foreach (var month in _months)
                                month.Setup();
                        }
                        if (data.BudgetPlanned != null)
                            budgetPlanned = data.BudgetPlanned;
                    }

                    IsDataLoaded = true;
                    BudgetDataChanged?.Invoke(true);
                    Task.Run(() => Save(false));
                }
            }
            catch (Exception exc)
            {
                LogsManager.Instance.WriteLine("SynchronizeData exception: " + exc.Message);
                LogsManager.Instance.WriteLine(exc.StackTrace);
            }
        }

        private void SynchronizeError(object sender, EventArgs args)
        {
            LoadAsync();
        }

        public async Task AddExpense(float value, DateTime date, int categoryID, int subcatID)
        {
            var month = GetMonth(date);
            month.AddExpense(value, date, categoryID, subcatID);
            await Save().ConfigureAwait(false);
        }

        public async Task AddIncome(float value, DateTime date, int incomeCategoryId)
        {
            var month = GetMonth(date);
            month.AddIncome(value, date, incomeCategoryId);
            await Save().ConfigureAwait(false);
        }

        public async Task UpdateMainPlannedBudget()
        {
            budgetPlanned = GetCurrentMonthData().BudgetPlanned;
            await Save().ConfigureAwait(false);
        }

        public double GetTotalPlannedExpensesForCurrentMonth()
        {
            return GetCurrentMonthData().GetTotalExpensesPlanned();
        }

        public double GetTotalPlannedIncomeForCurrentMonth()
        {
            return GetCurrentMonthData().GetTotalIncomePlanned();
        }

        public BudgetMonth GetMonth(DateTime date)
        {
            var month = _months.Find(x => x.Month == date.Month && x.Year == date.Year);
            if (month == null)
            {
                month = BudgetMonth.Create(BudgetDescription.Categories, date);
                month.onBudgetPlannedChanged += OnPlannedBudgetChanged;
                month.UpdatePlannedBudget(budgetPlanned);
                month.Setup();
                _months.Add(month);
                //??
                //Save();
            }

            return month;
        }

        public bool HasMonthData(DateTime date) => _months.Find(x => x.Month == date.Month && x.Year == date.Year) != null;

        public ObservableCollection<BudgetMonth.BudgetChartData> GetCurrentMonthChartData()
        {
            return GetMonth(DateTime.Now).GetData();
        }

        public BudgetMonth GetCurrentMonthData()
        {
            return GetMonth(DateTime.Now);
        }
    }
}

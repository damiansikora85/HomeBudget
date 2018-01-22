﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeBudget.Code
{
    public class BudgetIncomeTemplate
    {
        [JsonProperty("name")]
        public string Name;

        [JsonProperty("id")]
        public int Id;

        [JsonProperty("icon")]
        public string IconFileName;
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FrameWork;

namespace Common.Database.World.Battlefront
{
    // Fixed value of a character 
    [DataTable(PreCache = false, TableName = "rvr_metrics", DatabaseName = "World", BindMethod = EBindingMethod.StaticBound)]
    [Serializable]
    public class RVRMetrics : DataObject
    {
        [PrimaryKey(AutoIncrement = true)]
        public int MetricId { get; set; }

        [DataElement(AllowDbNull = false)]
        public int Tier { get; set; }

        [DataElement(AllowDbNull = false)]
        public int BattlefrontId { get; set; }

        [DataElement(AllowDbNull = false)]
        public int OrderVictoryPoints { get; set; }

        [DataElement(AllowDbNull = false)]
        public int DestructionVictoryPoints { get; set; }

        [DataElement(AllowDbNull = false)]
        public string BattlefrontName { get; set; }

        [DataElement(AllowDbNull = false)]
        public int PlayersInLake { get; set; }

        [DataElement(AllowDbNull = false)]
        public int Locked { get; set; }
    }
}

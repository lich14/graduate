using System;
using System.Linq;
using ZECS.Schedule.DBDefine.Schedule;

namespace ZECS.Schedule.ECSSchedule
{
    public class Asc
    {
        public ASC_STATUS Status { get; set; }

        public string BlockNo { get; private set; }

        public bool IsWaterSide { get; private set; }

        /// <summary>
        /// 此ASC可协助另一ASC做任务
        /// </summary>
        public bool HelpMode
        {
            get
            {
                return (Status != null
                        && (string.Compare(Status.HELP_MODE, "YES", StringComparison.OrdinalIgnoreCase) == 0
                            || string.Compare(Status.HELP_MODE, "TRUE", StringComparison.OrdinalIgnoreCase) == 0)
                        );
            }
        }

        public Asc(ASC_STATUS status, AscConfig[] arrAscConfig)
        {
            Status = status;

            var ascConfig = arrAscConfig.FirstOrDefault(x => x.CheId == Status.CHE_ID);

            if (ascConfig == null)
            {
                throw new Exception("Error to find the Block No of ASC (CHE_ID is " + Status.CHE_ID + ")");
            }

            BlockNo = ascConfig.BlockNo;
            IsWaterSide = ascConfig.IsWaterSide;
        }

        public bool CanBeScheduled()
        {
            if (Status.OPERATIONAL_STATUS != Operational_Status.AUTOMATIC)
            {
                return false;
            }

            if (Status.TECHNICAL_STATUS == Technical_Status.RED
                || Status.TECHNICAL_STATUS == Technical_Status.ORANGE)
            {
                return false;
            }

            return true;
        }

        public bool IsMaintenaceMode()
        {
            return Status.OPERATIONAL_STATUS == Operational_Status.MAINTENANCE_MODE;
        }

        public static AscConfig[] LoadConfig(string ascConfigFile = null)
        {
            // load config from file
            // temporarily hard code the Block No <-->CHE_ID // todo
            var arrAscConfig = new AscConfig[]
            {
                new AscConfig("262", "A02", true),
                new AscConfig("263", "A03", true),
                new AscConfig("264", "A04", true),
                new AscConfig("265", "A05", true),
                new AscConfig("266", "A06", true),
                new AscConfig("267", "A07", true),
                new AscConfig("268", "A08", true),
                new AscConfig("269", "A09", true),

                new AscConfig("272", "A02", false),
                new AscConfig("273", "A03", false),
                new AscConfig("274", "A04", false),
                new AscConfig("275", "A05", false),
                new AscConfig("276", "A06", false),
                new AscConfig("277", "A07", false),
                new AscConfig("278", "A08", false),
                new AscConfig("279", "A09", false)
            };

            return arrAscConfig;
        }

        public override string ToString()
        {
            return Status != null ? Status.ToString() : "";
        }
    }

    public class AscConfig
    {
        public AscConfig(string cheId, string blockNo, bool isWaterSide)
        {
            CheId = cheId;
            BlockNo = blockNo;
            IsWaterSide = isWaterSide;
        }
        public string CheId { get; private set; }
        public string BlockNo { get; private set; }
        public bool IsWaterSide { get; private set; }
    }
}
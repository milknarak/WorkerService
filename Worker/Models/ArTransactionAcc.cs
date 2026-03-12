using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Worker.Models
{
    public class ArTransactionAcc
    {
        public string group_id { get; set; }
        public int acc_seq { get; set; }
        public int seq { get; set; }
        public string acc_code { get; set; }
        public string div_code { get; set; }
        public string ou_det { get; set; }
        public decimal? dr_amt { get; set; }
        public decimal? cr_amt { get; set; }
        public string remark { get; set; }
    }
}

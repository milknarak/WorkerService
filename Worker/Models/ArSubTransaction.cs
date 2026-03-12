using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Worker.Models
{
    public class ArSubTransaction
    {
        public string group_id { get; set; }
        public int tran_seq { get; set; }
        public int seq { get; set; }
        public string rev_exp_code { get; set; }
        public string div_code { get; set; }
        public string ou_det { get; set; }
        public decimal? curr_amt { get; set; }
        public decimal? local_amt { get; set; }
        public string note { get; set; }
    }
}

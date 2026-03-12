using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Worker.Models
{
    public class SapPayload
    {
        public ApTransaction apTransaction { get; set; }
        public ArTransaction arTransaction { get; set; }
    }
}

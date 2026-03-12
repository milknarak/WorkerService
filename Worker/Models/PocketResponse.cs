using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Worker.Models
{
    public class PocketResponse<T>
    {
        public List<T> items { get; set; }
    }
}

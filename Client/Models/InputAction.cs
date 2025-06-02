using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Models
{
    public class InputAction
    {
        public string Type { get; set; }
        public string Action { get; set; }
        public string Key { get; set; }
        public string Button { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public string[] Modifiers { get; set; }
    }

}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhantomGo.Models
{
    public class Move
    {
        public int Id { get; set; }
        public string player { get; set; }
        public string message { get; set; }

        public string idAndMessage => $"{Id} {message}";
    }
}

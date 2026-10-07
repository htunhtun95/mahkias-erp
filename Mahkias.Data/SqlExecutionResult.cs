using System;
using System.Collections.Generic;
using System.Text;

namespace Mahkias.Data
{
    public class SqlExecutionResult
    {
        public bool Successful { get; set; }
        public int IntResult { get; set; }
        public List<KeyValuePair<string, int>> IntResults { get; set; }
    }
}

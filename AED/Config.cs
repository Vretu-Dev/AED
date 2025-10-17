using Exiled.API.Interfaces;
using System.Collections.Generic;

namespace AED
{
    public class Config : IConfig
    {
        public bool IsEnabled { get; set; } = true;
        public bool Debug { get; set; } = false;
        public List<AED> AED { get; private set; } = new()
        {
            new AED()
        };
    }
}
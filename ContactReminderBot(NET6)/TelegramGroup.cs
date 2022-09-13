using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContactReminderBot_NET6_
{
    internal class TelegramGroup
    {
        public long ID { get; set; }
        public string Name { get; set; }
        public string TextTemplate { get; set; }
        public TelegramGroup(long iD, string name)
        {
            ID = iD;
            Name = name;
            TextTemplate = "No template";
        }
        public TelegramGroup() : this(-1, "No name")
        { }
    }
}

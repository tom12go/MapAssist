using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace MapAssist.Settings
{
    public class ItemLogConfiguration
    {
        [YamlMember(Alias = "Enabled", ApplyNamingConventions = false)]
        public bool Enabled { get; set; }

        [YamlMember(Alias = "FilterFileName", ApplyNamingConventions = false)]
        public string FilterFileName { get; set; }

        [YamlMember(Alias = "PlaySoundOnDrop", ApplyNamingConventions = false)]
        public bool PlaySoundOnDrop { get; set; }

        [YamlMember(Alias = "DisplayForSeconds", ApplyNamingConventions = false)]
        public double DisplayForSeconds { get; set; }
        [YamlMember(Alias = "SoundFile", ApplyNamingConventions = false)]
        public string SoundFile { get; set; }

        [YamlMember(Alias = "LabelFont", ApplyNamingConventions = false)]
        public string LabelFont { get; set; }

        [YamlMember(Alias = "LabelFontSize", ApplyNamingConventions = false)]
        public int LabelFontSize { get; set; }
    }
}

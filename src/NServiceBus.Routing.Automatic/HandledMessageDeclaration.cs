﻿using System.Collections.Generic;

namespace NServiceBus.Routing.Automatic
{
    public class HandledMessageDeclaration
    {
        public string EndpointName { get; set; }
        public string Discriminator { get; set; }
        public Dictionary<string, string> InstanceProperties { get; set; }
        public string[] HandledMessageTypes { get; set; } = new string[0];
        public string[] PublishedMessageTypes { get; set; } = new string[0];
    }
}
using Exiled.API.Features;
using Exiled.CustomItems.API.Features;
using System;
using System.Collections.Generic;

namespace AED
{
    public class Plugin : Plugin<Config>
    {
        public override string Name => "Automated.External.Defibrillator";
        public override string Author => "Vretu";
        public override string Prefix => "AED";
        public override Version Version => new Version(1, 0, 0);
        public override Version RequiredExiledVersion { get; } = new Version(9, 9, 0);
        public static Plugin Instance { get; private set; }

        public override void OnEnabled()
        {
            CustomItem.RegisterItems();
            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            CustomItem.UnregisterItems();
            base.OnDisabled();
        }
    }
}
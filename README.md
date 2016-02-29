# SMAPI
A Modding API For Stardew Valley

You can create a mod by making a direct reference to the ModdingApi.exe

From there, you need to inherit from StardewModdingAPI.Mod

The first class that inherits from that class will be loaded into the game at runtime, and once the game fully initializes the mod, the method Entry() will be called once.

It is recommended to subscribe to an event (from Events.cs) to be able to interface with the game rather than directly make changes from the Entry() method.


    TestMod.cs:
    
        public class TestMod : Mod
        {
            public override string Name
            {
                get { return "Test Mod"; }
            }

            public override string Authour
            {
                get { return "Zoryn Aaron"; }
            }

            public override string Version
            {
                get { return "0.0.1Test"; }
            }

            public override string Description
            {
                get { return "A Test Mod"; }
            }

            public override void Entry()
            {
                Console.WriteLine("Test Mod Has Loaded");
                Program.LogError("Test Mod can call to Program.cs in the API");
                Program.LogColour(ConsoleColor.Magenta, "Test Mod is just a tiny DLL file in AppData/Roaming/StardewValley/Mods");

                Events.GameLoaded += Events_GameLoaded;
            }

            void Events_GameLoaded()
            {
                
                Program.LogInfo("[Game Loaded Event] I can do things directly to the game now that I am certain it is loaded thanks to events.");
            }
        }
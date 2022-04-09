using System.Net;
using Haukcode.ArtNet;
using Haukcode.ArtNet.Packets;
using Haukcode.ArtNet.Sockets;
using Haukcode.Sockets;
using MagicHome;

namespace MyApp // Note: actual namespace depends on the project name.
{
    internal class Program
    {
        static ArtNetSocket socket;
        static short universe;
        static short startChannel;
        static Light light;

        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            var configuration = ProcessArgs(args);
            universe = short.Parse(configuration.ContainsKey("--universe") ? configuration["--universe"] : configuration.ContainsKey("-u") ? configuration["-u"] : "-1");
            if (universe < 0)
            {
                Console.WriteLine("Invalid Universe");
                Environment.Exit(1);
                return;
            }
            startChannel = short.Parse(configuration.ContainsKey("--channel") ? configuration["--channel"] : configuration.ContainsKey("-c") ? configuration["-c"] : "-1");
            if (startChannel < 1 || startChannel > 255)
            {
                Console.WriteLine("Invalid start channel");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine("Finding Light");
            var lights = await Light.DiscoverAsync();
            if (!lights.Any())
            {
                Console.WriteLine("No Lights Found");
                Environment.Exit(1);
                return;
            }
            Console.WriteLine($"Found {lights.Count} lights");

            light = lights.First();
            await light.ConnectAsync();
            await light.TurnOnAsync();
            await light.SetColorAsync(0, 0, 0);
            Console.WriteLine($"Connected to light");

            Console.WriteLine("Starting Art-Net Listener");
            socket = new ArtNetSocket
            {
                EnableBroadcast = true,
            };
            socket.NewPacket += HandleNewPacket;
            socket.Open(IPAddress.Any, IPAddress.Broadcast);
            Console.WriteLine("Art-Net Listener Ready");

            await Task.Delay(-1);
        }

        static Dictionary<string, string> ProcessArgs(string[] args)
        {
            var config = new Dictionary<string, string>();
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (!arg.StartsWith("-") || args.Length - 1 < i + 1) continue;

                config.Add(arg, args[i + 1]);
            }

            return config;
        }

        static async void HandleNewPacket(object sender, NewPacketEventArgs<ArtNetPacket> e)
        {
            if (e.Packet.OpCode != ArtNetOpCodes.Dmx) return;

            var packet = e.Packet as ArtNetDmxPacket;
            if (packet.Universe == universe)
            {
                double intensity = (double)(packet.DmxData[startChannel - 1]) / 255;
                byte red = (byte)(packet.DmxData[startChannel] * intensity);
                byte green = (byte)(packet.DmxData[startChannel + 1] * intensity);
                byte blue = (byte)(packet.DmxData[startChannel + 2] * intensity);
                byte preset = packet.DmxData[startChannel + 3];
                byte presetSpeed = (byte)(packet.DmxData[startChannel + 4] / 255 * 100);

                if (!light.Power)
                {
                    await light.TurnOnAsync();
                }

                if (preset > 10)
                {
                    PresetPattern? pattern = null;
                    if (preset < 21)
                    {
                        pattern = PresetPattern.SevenColorsCrossFade;
                    }
                    else if (preset < 31)
                    {
                        pattern = PresetPattern.RedGradualChange;
                    }
                    else if (preset < 41)
                    {
                        pattern = PresetPattern.GreenGradualChange;
                    }
                    else if (preset < 51)
                    {
                        pattern = PresetPattern.BlueGradualChange;
                    }
                    else if (preset < 61)
                    {
                        pattern = PresetPattern.YellowGradualChange;
                    }
                    else if (preset < 71)
                    {
                        pattern = PresetPattern.CyanGradualChange;
                    }
                    else if (preset < 81)
                    {
                        pattern = PresetPattern.PurpleGradualChange;
                    }
                    else if (preset < 91)
                    {
                        pattern = PresetPattern.WhiteGradualChange;
                    }
                    else if (preset < 101)
                    {
                        pattern = PresetPattern.RedGreenCrossFade;
                    }
                    else if (preset < 111)
                    {
                        pattern = PresetPattern.SevenColorStrobeFlash;
                    }
                    else if (preset < 121)
                    {
                        pattern = PresetPattern.RedStrobeFlash;
                    }
                    else if (preset < 131)
                    {
                        pattern = PresetPattern.GreenStrobeFlash;
                    }
                    else if (preset < 141)
                    {
                        pattern = PresetPattern.BlueStrobeFlash;
                    }
                    else if (preset < 151)
                    {
                        pattern = PresetPattern.YellowStrobeFlash;
                    }
                    else if (preset < 161)
                    {
                        pattern = PresetPattern.CyanStrobeFlash;
                    }
                    else if (preset < 171)
                    {
                        pattern = PresetPattern.PurpleStrobeFlash;
                    }
                    else if (preset < 181)
                    {
                        pattern = PresetPattern.WhiteStrobeFlash;
                    }
                    else if (preset < 191)
                    {
                        pattern = PresetPattern.SevenColorsJumping;
                    }

                    if (pattern != null)
                    {
                        await light.SetPresetPatternAsync(pattern.Value, presetSpeed);
                        return;
                    }
                }

                if (light.Color.Red == red && light.Color.Green == green && light.Color.Blue == blue) return;

                await light.SetColorAsync(red, green, blue);
            }
        }
    }
}
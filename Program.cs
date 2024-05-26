// See https://aka.ms/new-console-template for more information
using DroxtalWolf;
using System.Diagnostics;
using MathNet.Numerics.Random;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DWClient;

internal class Program
{
    public static void Main(string[] args)
    {
        /* DWClient uses DroxtalWolf to simulate movement of points
        through space under the influence of a wind field. */
        Console.WriteLine("Initiating DroxtalWolf simulation.");

        // Initialize timing
        Stopwatch watch = new();
        watch.Start();
        Dictionary<string,Stopwatch> subwatches = [];
        foreach (string watchName in (string[])["Point seeding", "Point physics", "Point culling", "Met advance",
                     "Derived quantities", "Met interpolate", "Archiving", "File writing"])
        {
            subwatches.Add(watchName, new Stopwatch());   
        }
        
        // Read in first argument as configuration file (or use default)
        string configFile = "config.yaml";
        if (args.Length > 0)
        {
            configFile = args[0];
        }
        RunOptions configOptions = RunOptions.ReadConfig(configFile);
        
        // Extract and store relevant variables
        bool verbose = configOptions.Verbose;
        bool updateMeteorology = configOptions.TimeDependentMeteorology;

        // Specify the domain
        double[] lonLims = configOptions.Domain.LonLimits;
        double[] latLims = configOptions.Domain.LatLimits;
        double[] pLims   = [configOptions.Domain.PressureBase * 100.0,
            configOptions.Domain.PressureCeiling * 100.0];

        // Major simulation settings
        DateTime startDate = configOptions.Timing.StartDate;
        DateTime endDate = configOptions.Timing.EndDate;
        // Time step in seconds
        double dt = configOptions.Timesteps.Simulation;
        // How often to add data to the in-memory archive?
        double dtStorage = 60.0 * configOptions.Timesteps.Storage;
        // How often to report to the user?
        double dtReport = 60.0 * configOptions.Timesteps.Reporting;
        // How often to write the in-memory archive to disk?
        //double dtOutput = TimeSpan.ParseExact(configOptions.Timesteps.Output,"hhmmss",CultureInfo.InvariantCulture).TotalSeconds;
        double dtOutput = RunOptions.ParseHms(configOptions.Timesteps.Output);
            
        DateTime currentDate = startDate; // DateTime is a value type so this creates a new copy
        
        // Check if the domain manager will need to calculate box heights (expensive)
        bool boxHeightsNeeded = configOptions.PointsFlights is { Active: true, ComplexContrails: true };
        
        // Are we using MERRA-2 or ERA5 data?
        //TODO: Move AP and BP out of here/MERRA-2 into MetManager, then delete MERRA2
        string dataSource = configOptions.InputOutput.MetSource;
        double[] AP, BP;
        bool fixedPressures;
        if (dataSource == "MERRA-2")
        {
            AP = MERRA2.AP;
            BP = MERRA2.BP;
            fixedPressures = false;
        }
        else if (dataSource == "ERA5")
        {
            AP = [ 70.0e2, 100.0e2, 125.0e2, 150.0e2, 175.0e2,
                  200.0e2, 225.0e2, 250.0e2, 300.0e2, 350.0e2,
                  400.0e2, 450.0e2, 500.0e2, 550.0e2, 600.0e2,
                  650.0e2, 700.0e2, 750.0e2, 775.0e2, 800.0e2,
                  825.0e2, 850.0e2, 875.0e2, 900.0e2, 925.0e2,
                  950.0e2, 975.0e2,1000.0e2];
            Array.Reverse(AP); // All data will be flipped internally
            BP = new double[AP.Length];
            for (int i = 0; i < AP.Length; i++)
            {
                BP[i] = 0.0;
            }
            fixedPressures = true;
        }
        else
        {
            throw new ArgumentException($"Meteorology data source {dataSource} not recognized.");
        }
    }
}
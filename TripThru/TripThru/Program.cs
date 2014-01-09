using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// Local
using Simulator.APIs;
using Simulator.Simulations;
using Simulator.TripThru;
using Simulator.Utils;

namespace Program
{
    class Program
    {
        static void Main(string[] args)
        {
            MapTools.LoadGeoData();
            MapTools.WriteGeoData();

            TripThru tripthru = new TripThru();
            List<Partner> partners = new List<Partner>();

            List<VehicleType> vehicleTypes = new List<VehicleType>();
            vehicleTypes.Add(VehicleType.Compact);
            vehicleTypes.Add(VehicleType.Sedan);

            {
                List<Fleet> fleets = new List<Fleet>();
                {
                    List<Pair<Location, Location>> trips = new List<Pair<Location, Location>>();
                    trips.Add(new Pair<Location, Location>(new Location(37.782551, -122.445368), new Location(37.786956, -122.440279))); // San Francisco (0)
                    trips.Add(new Pair<Location, Location>(new Location(37.800224, -122.43352), new Location(37.800066, -122.436167))); // San Francisco (1)
                    trips.Add(new Pair<Location, Location>(new Location(37.784345, -122.422922), new Location(37.785292, -122.416257))); // San Francisco (1)
                    trips.Add(new Pair<Location, Location>(new Location(48.835975, 2.345097), new Location(48.837275, 2.382433))); // Paris
                    trips.Add(new Pair<Location, Location>(new Location(48.843545, 2.385352), new Location(48.839478, 2.317374))); // Paris
                    trips.Add(new Pair<Location, Location>(new Location(25.270751, 55.314030), new Location(25.279288, 55.304331))); // Dubai
                    trips.Add(new Pair<Location, Location>(new Location(-22.910194, -43.212211), new Location(-22.900311, -43.240621))); // Rio
                    trips.Add(new Pair<Location, Location>(new Location(42.342634, -71.122545), new Location(42.367561, -71.129498))); // Boston (63)

                    //Location location = new Location(37.745460, -122.400551);
                    Location location = new Location(37.78906, -122.402127);
                    Zone zone = new Zone(location, 50);
                    List<Driver> drivers = new List<Driver>();
                    drivers.Add(new Driver("Alex Goldman"));
                    drivers.Add(new Driver("Jason Fama"));
                    drivers.Add(new Driver("Simon Shvarts"));
                    List<Zone> coverage = new List<Zone>();
                    coverage.Add(zone);

                    // These are just random routes for simulation to pick from
                    List<Passenger> passengers = new List<Passenger>();
                    passengers.Add(new Passenger("Elvis Presley"));
                    passengers.Add(new Passenger("John Riggins"));
                    passengers.Add(new Passenger("Bart Star"));
                    passengers.Add(new Passenger("Michelle Phieffer"));
                    passengers.Add(new Passenger("Zong Zi Yi"));
                    passengers.Add(new Passenger("Mickey Rourke"));


                    Fleet fleet = new Fleet(
                        name: "Luxor Cab - SF",
                        location: location, 
                        coverage: coverage, 
                        drivers: drivers, 
                        vehicleTypes: vehicleTypes, 
                        possibleTrips: trips, 
                        baseCost: 3.00,
                        costPerMile: 2.70, 
                        tripsPerHour: 200, 
                        passengers: passengers);

                    fleets.Add(fleet);


                }
                partners.Add(new Partner(tripthru: tripthru, name: "Luxor Cab", fleets: fleets));
            }
            {
                List<Fleet> fleets = new List<Fleet>();
                {
                    List<Pair<Location, Location>> trips = new List<Pair<Location, Location>>();
                    trips.Add(new Pair<Location, Location>(new Location(37.782551, -122.445368), new Location(37.786956, -122.440279)));
//                    Location location = new Location(37.751176, -122.394278);
                    Location location = new Location(37.78906, -122.402127);
                    Zone zone = new Zone(location, 50);
                    List<Driver> drivers = new List<Driver>();
                    drivers.Add(new Driver("Eduardo Lozano"));
                    drivers.Add(new Driver("Edward Hamilton"));
                    drivers.Add(new Driver("Steven Thompson"));

                    // These are just random routes for simulation to pick from
                    List<Passenger> passengers = new List<Passenger>();
                    passengers.Add(new Passenger("George Washington"));
                    passengers.Add(new Passenger("Abraham Lincoln"));
                    passengers.Add(new Passenger("Herbert Hoover"));
                    passengers.Add(new Passenger("John Kennedy"));
                    passengers.Add(new Passenger("Jimmy Carter"));
                    passengers.Add(new Passenger("Richard Nixon"));


                    List<Zone> coverage = new List<Zone>();
                    coverage.Add(zone);
                    Fleet fleet = new Fleet(
                        name: "Yellow Cab - SF",
                        location: location,
                        coverage: coverage,
                        drivers: drivers, vehicleTypes: vehicleTypes,
                        possibleTrips: trips,
                        baseCost: 3.00, 
                        costPerMile: 3.00, 
                        tripsPerHour: 20, 
                        passengers: passengers);
                    fleets.Add(fleet);


                }
                partners.Add(new Partner(tripthru: tripthru, name: "Yellow Cab", fleets: fleets));
            }

            {
                List<Fleet> fleets = new List<Fleet>();
                {
                    List<Pair<Location, Location>> trips = new List<Pair<Location, Location>>();
                    trips.Add(new Pair<Location, Location>(new Location(37.782551, -122.445368), new Location(37.786956, -122.440279))); // San Francisco
                    trips.Add(new Pair<Location, Location>(new Location(42.342634, -71.122545), new Location(42.367561, -71.129498))); // Boston / Braintree
                    //                    Location location = new Location(37.751176, -122.394278);
                    Location location = new Location(42.356217, -71.137512);
                    Zone zone = new Zone(location, 50);
                    List<Driver> drivers = new List<Driver>();
                    drivers.Add(new Driver("Joanna Glennon"));
                    drivers.Add(new Driver("Ofer Matan"));

                    // These are just random routes for simulation to pick from
                    List<Passenger> passengers = new List<Passenger>();
                    passengers.Add(new Passenger("Michael Glennon"));
                    passengers.Add(new Passenger("William Glennon"));
                    passengers.Add(new Passenger("Bernice Hamilton"));


                    List<Zone> coverage = new List<Zone>();
                    coverage.Add(zone);
                    Fleet fleet = new Fleet(
                        name: "Metro Cab of Boston",
                        location: location,
                        coverage: coverage,
                        drivers: drivers, vehicleTypes: vehicleTypes,
                        possibleTrips: trips,
                        baseCost: 3.00, 
                        costPerMile: 3.00, 
                        tripsPerHour: 20,
                        passengers: passengers);
                    fleets.Add(fleet);


                }
                partners.Add(new Partner(tripthru: tripthru, name: "Metro Cab of Boston", fleets: fleets));
            }

            {
                List<Fleet> fleets = new List<Fleet>();
                {
                    List<Pair<Location, Location>> trips = new List<Pair<Location, Location>>();
                    //                    Location location = new Location(37.751176, -122.394278);
                    trips.Add(new Pair<Location, Location>(new Location(37.784345, -122.422922), new Location(37.785292, -122.416257))); // San Francisco (1)
                    trips.Add(new Pair<Location, Location>(new Location(48.835975, 2.345097), new Location(48.837275, 2.382433))); // Paris
                    Location location = new Location(48.837246, 2.347844);
                    Zone zone = new Zone(location, 50);
                    List<Driver> drivers = new List<Driver>();
                    drivers.Add(new Driver("Slyvian Reubele"));
                    drivers.Add(new Driver("Wassem Mohammed"));

                    // These are just random routes for simulation to pick from
                    List<Passenger> passengers = new List<Passenger>();
                    passengers.Add(new Passenger("Daiel Corona"));


                    List<Zone> coverage = new List<Zone>();
                    coverage.Add(zone);
                    Fleet fleet = new Fleet(
                        name: "Les Taxi Blues",
                        location: location,
                        coverage: coverage,
                        drivers: drivers, vehicleTypes: vehicleTypes,
                        possibleTrips: trips,
                        baseCost: 5.00,
                        costPerMile: 4.70,
                        tripsPerHour: 20,
                        passengers: passengers);
                    fleets.Add(fleet);


                }
                partners.Add(new Partner(tripthru: tripthru, name: "Les Taxi Blues", fleets: fleets));
            }

            {
                List<Fleet> fleets = new List<Fleet>();
                {
                    List<Pair<Location, Location>> trips = new List<Pair<Location, Location>>();
                    //                    Location location = new Location(37.751176, -122.394278);
                    trips.Add(new Pair<Location, Location>(new Location(25.270751, 55.314030), new Location(25.279288, 55.304331))); // Dubai
                    Location location = new Location(25.271139, 55.307485);
                    Zone zone = new Zone(location, 50);
                    List<Driver> drivers = new List<Driver>();
                    drivers.Add(new Driver("Hussean Wiobe"));
                    drivers.Add(new Driver("Omar Sharief"));

                    // These are just random routes for simulation to pick from
                    List<Passenger> passengers = new List<Passenger>();
                    passengers.Add(new Passenger("Sheikh Hamdan Bin Mohammed bin Rashid Al Maktoum"));


                    List<Zone> coverage = new List<Zone>();
                    coverage.Add(zone);
                    Fleet fleet = new Fleet(
                        name: "Dubai Taxi Corporation",
                        location: location,
                        coverage: coverage,
                        drivers: drivers, vehicleTypes: vehicleTypes,
                        possibleTrips: trips,
                        baseCost: 3.00,
                        costPerMile: 3.00,
                        tripsPerHour: 20,
                        passengers: passengers);
                    fleets.Add(fleet);


                }
                partners.Add(new Partner(tripthru: tripthru, name: "Dubai Taxi Corporation", fleets: fleets));
            }


            MapTools.WriteGeoData();

            Simulate(partners, DateTime.UtcNow + new TimeSpan(0, 30, 0));
            MapTools.WriteGeoData();
        }
        public static void Simulate(List<Partner> partners, DateTime until)
        {
            Logger.OpenLog("TripThruSimulation.log", true);

            Logger.Log("Sim Configuration");
            Logger.Tab();
            foreach (Partner p in partners)
                p.Log();
            Logger.Untab();

            Logger.Log("Simulating from " + DateTime.UtcNow + " until " + until.ToString());
            Logger.Tab();
            TimeSpan simInterval = new TimeSpan(0, 0, 10);
            while (DateTime.UtcNow < until)
            {
                Logger.Log("Time = " + DateTime.UtcNow);
                Logger.Tab();
                foreach (Partner p in partners)
                    p.Simulate(until);
                MapTools.WriteGeoData();
                System.Threading.Thread.Sleep(simInterval);
                Logger.Untab();
            }
            Logger.Untab();

        }
    }
}

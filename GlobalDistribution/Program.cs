using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace GlobalDistribution
{
  class Program
  {
    static void Main(string[] args)
    {
      //Demo.Menu(); return;
      while (true)
      {
        Console.WriteLine("Cosmos DB geo-replication demo");
        Console.WriteLine();
        Console.Write("Init / Query / Read / Write / Conflict / Delete / Exit: ");
        var input = Console.ReadLine().Trim().ToUpper().Substring(0);
        if (input == "I") InitDemo().Wait();
        if (input == "Q") QueryDemo().Wait();
        if (input == "R") ReadDemo().Wait();
        if (input == "W") WriteDemo().Wait();
        if (input == "C") ConflictDemo().Wait();
        if (input == "D") DeleteDemo().Wait();
        if (input == "E") break;
      }
    }

    private static CosmosClient GetClient()
    {
      var config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

      var endpoint = config["CosmosEndpoint"];
      var masterKey = config["CosmosMasterKey"];
      var client = new CosmosClient(endpoint, masterKey);

      /*WITH GLOBAL REPLICATION >this makes our application works in all regions with failover. but specify which region to prefer.
       * ie, whicherver region that the application instane is running in. get region name "CosmosRegion" from config
       
      var options = new CosmosClientOptions { ApplicationRegion = config["CosmosRegion"] };
      var client = new CosmosClient(endpoint, masterKey, options);
      */

      return client;
    }

    private static async Task InitDemo()
    {
      var client = GetClient();

      await client.GetDatabase("Families").DeleteAsync();

      var database = (await client.CreateDatabaseAsync("Families")).Database;

      var containerDef = new ContainerProperties
      {
        Id = "Families",
        PartitionKeyPath = "/address/zipCode",
        ConflictResolutionPolicy = new ConflictResolutionPolicy
        {
          Mode = ConflictResolutionMode.Custom,
          ResolutionProcedure = "dbs/Families/colls/Families/sprocs/resolveConflict/"
        }
      };
      var container = (await database.CreateContainerAsync(containerDef, 1000)).Container;

      dynamic docDef = new
      {
        id = "Sample",
        familyName = "Jones",
        address = new
        {
          addressLine = "456 Harbor Boulevard",
          city = "Chicago",
          state = "IL",
          zipCode = "60603"
        },
        parents = new string[]
        {
          "David",
          "Diana"
        },
        kids = new string[]
        {
          "Evan"
        },
        pets = new string[]
        {
          "Lint"
        }
      };

      await container.CreateItemAsync(docDef, new PartitionKey(docDef.address.zipCode.ToString()));

      Console.WriteLine($"Demo database has been initialized");
      Console.WriteLine();
    }

        //rectrive same josn document from cosmosDB count times.
        private static async Task QueryDemo()
    {
      var count = GetCount();

      using (var client = GetClient())
      {
        var totalStartedAt = DateTime.Now;
        var container = client.GetContainer("Families", "Families");
        for (var i = 0; i < count; i++)
        {
          var iterator = container.GetItemQueryIterator<dynamic>(
            queryText: "SELECT * FROM c WHERE c.address.zipCode = '60603'",
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey("60603") } //anytime if u know partition key then set it explicitly. so that it runs as single partition query 
          );
          var startedAt = DateTime.Now;
          var result = await iterator.ReadNextAsync();
          var elapsed = DateTime.Now.Subtract(startedAt).TotalMilliseconds;
          var cost = result.RequestCharge;

          Console.WriteLine($"Query {i + 1}. {result.First().familyName}. Elapsed: {elapsed} ms; Cost: {cost} RUs");
        }
        var totalElapsed = DateTime.Now.Subtract(totalStartedAt).TotalMilliseconds;
        Console.WriteLine($"Total elapsed: {totalElapsed} ms");
        Console.WriteLine();
      }
    }

    private static async Task ReadDemo()
    {
      var count = GetCount();

      using (var client = GetClient())
      {
        var totalStartedAt = DateTime.Now;
        var container = client.GetContainer("Families", "Families");
        for (var i = 0; i < count; i++)
        {
          var startedAt = DateTime.Now;
          var result = await container.ReadItemAsync<dynamic>("Sample", new PartitionKey("60603"));
          var elapsed = DateTime.Now.Subtract(startedAt).TotalMilliseconds;
          var cost = result.RequestCharge;

          Console.WriteLine($"Read {i + 1}. {result.Resource["familyName"]}. Elapsed: {elapsed} ms; Cost: {cost} RUs");
        }
        var totalElapsed = DateTime.Now.Subtract(totalStartedAt).TotalMilliseconds;
        Console.WriteLine($"Total elapsed: {totalElapsed} ms");
        Console.WriteLine();
      }
    }

    private static async Task WriteDemo()
    {
      var count = GetCount();

      using (var client = GetClient())
      {
        var container = client.GetContainer("Families", "Families");
        var readResult = await container.ReadItemAsync<dynamic>("Sample", new PartitionKey("60603"));
        dynamic doc = readResult.Resource;

        var totalStartedAt = DateTime.Now;
        for (var i = 0; i < count; i++)
        {
          doc.familyName = $"Jones {Guid.NewGuid()}";
          var startedAt = DateTime.Now;
          var result = await container.ReplaceItemAsync(doc, "Sample");
          var elapsed = DateTime.Now.Subtract(startedAt).TotalMilliseconds;
          var cost = result.RequestCharge;

          Console.WriteLine($"Write {i + 1}. {doc.familyName}. Elapsed: {elapsed} ms; Cost: {cost} RUs");
        }
        var totalElapsed = DateTime.Now.Subtract(totalStartedAt).TotalMilliseconds;
        Console.WriteLine($"Total elapsed: {totalElapsed} ms");
        Console.WriteLine();
      }
    }

    //running this code simultaneously in different regions generates conflicts, results in conflict for global distribution
    //its works on the basis that the clocks are synchronized to the second between the VMs in westUs and Southeast asia, even though there in different timezone
    private static async Task ConflictDemo()
    {
      var count = GetCount();

      using (var client = GetClient())
      {
        var container = client.GetContainer("Families", "Families");
        for (var i = 0; i < count; i++)
        {
          var readResult = await container.ReadItemAsync<dynamic>("Sample", new PartitionKey("60603"));
          dynamic doc = readResult.Resource;

          doc.familyName = $"Jones {Environment.MachineName} {i}"; //replace famility name with machine name and loop count to know which region wins when there is conflict

          if (i == 0)
          {
            Console.WriteLine($"Waiting {60 - DateTime.Now.Second} seconds for next minute interval for simultaneous update");
            while (DateTime.Now.Second != 0) { }//it waits until clock is 0 seconds on the next minute before replcing documents multiple times in loop
          }

          await container.ReplaceItemAsync(doc, "Sample");//replace document

          Console.WriteLine($"Updated Jones document with machine name '{Environment.MachineName} {i}'");
        }
        Console.WriteLine();
      }
    }

    private static async Task DeleteDemo()
    {
      var client = GetClient();
      await client.GetDatabase("Families").DeleteAsync();

      Console.WriteLine($"Demo database has been deleted");
      Console.WriteLine();
    }

    private static int GetCount()
    {
      while (true)
      {
        Console.Write("Iterations [100]: ");
        var countString = Console.ReadLine();
        if (countString.Trim().Length == 0)
        {
          return 100;
        }
        if (int.TryParse(countString, out int count))
        {
          return count;
        }
      }
    }

  }
}

using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CosmosDb.DotNetSdk.Demos
{
	//3.Working with Documents
	public static class DocumentsDemo
	{
		public async static Task Run()
		{
			Debugger.Break();

			await CreateDocuments();

			await QueryDocuments();

            await QueryWithStatefulPaging();
			await QueryWithStatelessPaging();

            await QueryWithStatefulPagingStreamed();
            await QueryWithStatelessPagingStreamed();

			QueryWithLinq();

			await ReplaceDocuments();

			await DeleteDocuments();
		}

		private async static Task CreateDocuments()
		{
			Console.Clear();
			Console.WriteLine(">>> Create Documents <<<");
			Console.WriteLine();

			var container = Shared.Client.GetContainer("mydb", "mystore");//or "adventure-works","stores"

			//document can be created using dynamic object
			dynamic document1Dynamic = new
			{
				id = Guid.NewGuid(),
				name = "New Customer 1",
				address = new
				{
					addressType = "Main Office",
					addressLine1 = "123 Main Street",
					location = new
					{
						city = "Brooklyn",
						stateProvinceName = "New York"
					},
					postalCode = "11229",
					countryRegionName = "United States"
				},
			};

			await container.CreateItemAsync(document1Dynamic, new PartitionKey("11229"));
			Console.WriteLine($"Created new document {document1Dynamic.id} from dynamic object");

			//document can be created using raw JSON string
			var document2Json = $@"
				{{
					""id"": ""{Guid.NewGuid()}"",
					""name"": ""New Customer 2"",
					""address"": {{
						""addressType"": ""Main Office"",
						""addressLine1"": ""123 Main Street"",
						""location"": {{
							""city"": ""Brooklyn"",
							""stateProvinceName"": ""New York""
						}},
						""postalCode"": ""11229"",
						""countryRegionName"": ""United States""
					}}
				}}";

			var document2Object = JsonConvert.DeserializeObject<JObject>(document2Json);
			await container.CreateItemAsync(document2Object, new PartitionKey("11229"));
			Console.WriteLine($"Created new document {document2Object["id"].Value<string>()} from JSON string");

			//document can be created using plain old CLR objects
			var document3Poco = new Customer
			{
				Id = Guid.NewGuid().ToString(),
				Name = "New Customer 3",
				Address = new Address
				{
					AddressType = "Main Office",
					AddressLine1 = "123 Main Street",
					Location = new Location
					{
						City = "Brooklyn",
						StateProvinceName = "New York"
					},
					PostalCode = "11229",
					CountryRegionName = "United States"
				},
			};

			await container.CreateItemAsync(document3Poco, new PartitionKey("11229"));
			Console.WriteLine($"Created new document {document3Poco.Id} from typed object");
		}

		private static async Task QueryDocuments()
		{
			Console.Clear();
			Console.WriteLine(">>> Query Documents (SQL) <<<");
			Console.WriteLine();

			var container = Shared.Client.GetContainer("mydb", "mystore");

			Console.WriteLine("Querying for new customer documents (SQL)");
			var sql = "SELECT * FROM c WHERE STARTSWITH(c.name, 'New Customer') = true";

			// Query for dynamic objects
			var iterator1 = container.GetItemQueryIterator<dynamic>(sql);//here no partiton key specified. but avoid this
			var documents1 = await iterator1.ReadNextAsync();
			var count = 0;
			foreach (var document in documents1)
			{
				Console.WriteLine($" ({++count}) Id: {document.id}; Name: {document.name};");

				// Dynamic object can be converted into a defined type...
				var customer = JsonConvert.DeserializeObject<Customer>(document.ToString());
				Console.WriteLine($"     City: {customer.Address.Location.City}");
			}
			Console.WriteLine($"Retrieved {count} new documents as dynamic");
			Console.WriteLine();

			// Or query for defined types; e.g., Customer
			var iterator2 = container.GetItemQueryIterator<Customer>(sql);
			var documents2 = await iterator2.ReadNextAsync();
			count = 0;
			foreach (var customer in documents2)
			{
				Console.WriteLine($" ({++count}) Id: {customer.Id}; Name: {customer.Name};");
				Console.WriteLine($"     City: {customer.Address.Location.City}");
			}
			Console.WriteLine($"Retrieved {count} new documents as Customer");
			Console.WriteLine();

			// You only get back the first "page" (up to MaxItemCount)
		}

		//for larger resultset ReadNextAsync only returns one page at a time.
		//maximum page size defaults to 100 documents. it can be overridden with query options
		//so, to get all data ReadNextAsync should called repeatedly. it can be 2 ways > stateful, stateless

		/*stateful approch> client issues single call to your application which then repeatedly calls for
		ReadNextAsync on the iterator until the iterator indicated you've exhausted all the results */
		private async static Task QueryWithStatefulPaging()
		{
			Console.Clear();
			Console.WriteLine(">>> Query Documents (paged results, stateful) <<<");
			Console.WriteLine();

			var container = Shared.Client.GetContainer("mydb", "mystore"); //"adventure-works","stores" >704 docs
			var sql = "SELECT * FROM c";//returns all documents in container

			// Get first page of large resultset
			Console.WriteLine("Querying for all documents (first page)");
			var iterator = container.GetItemQueryIterator<Customer>(sql,requestOptions:new QueryRequestOptions { MaxItemCount=100}); //default pagesize is 1000
			var documents = await iterator.ReadNextAsync();
			var itemCount = 0;
			foreach (var customer in documents)
			{
				Console.WriteLine($" ({++itemCount}) Id: {customer.Id}; Name: {customer.Name};");
			}
			Console.WriteLine($"Retrieved {itemCount} documents in first page");
			Console.WriteLine();

			// Get all pages of large resultset using iterator.HasMoreResults
			Console.WriteLine("Querying for all documents (full resultset, stateful)");
			iterator = container.GetItemQueryIterator<Customer>(sql, requestOptions: new QueryRequestOptions { MaxItemCount = 100 });
			itemCount = 0;
			var pageCount = 0;
			while (iterator.HasMoreResults)
			{
				pageCount++;
				documents = await iterator.ReadNextAsync();
				foreach (var customer in documents)
				{
					Console.WriteLine($" ({pageCount}.{++itemCount}) Id: {customer.Id}; Name: {customer.Name};");
				}
			}
			//for very large dataset, its a danger
			//stateless paging is better alternative
			Console.WriteLine($"Retrieved {itemCount} documents (full resultset, stateful)");
			Console.WriteLine();
		}

		/*stateless > only call ReadNextAsync once to get first page. then CosmosDB also returns speacial continuation token
		that you can return to client along with the page. so your application does not continue processing the rest of the result set. 
		instead client can request next page by supplying that continuation token along with your next request for your application to run 
		the same query.But given the continuation token COsmosDB returns next page of results based on where previous page left off*/
		private async static Task QueryWithStatelessPaging()
		{
			// Get all pages of large resultset using continuation token
			Console.WriteLine("Querying for all documents (full resultset, stateless)");

			var continuationToken = default(string);
			do
			{
				continuationToken = await QueryFetchNextPage(continuationToken);
			} while (continuationToken != null);//simulate multiple calls

			Console.WriteLine($"Retrieved all documents (full resultset, stateless)");
			Console.WriteLine();
		}

		private async static Task<string> QueryFetchNextPage(string continuationToken)
		{
			var container = Shared.Client.GetContainer("mydb", "mystore");
			var sql = "SELECT * FROM c";

			var iterator = container.GetItemQueryIterator<Customer>(sql, continuationToken);
			var page = await iterator.ReadNextAsync();
			var itemCount = 0;

			if (continuationToken != null)
			{
				Console.WriteLine($"... resuming with continuation {continuationToken}");
			}

			foreach (var customer in page)
			{
				Console.WriteLine($" ({++itemCount}) Id: {customer.Id}; Name: {customer.Name};");
			}

			continuationToken = page.ContinuationToken;

			if (continuationToken == null)
			{
				Console.WriteLine($"... no more continuation; resultset complete");
			}

			return continuationToken;
		}

		/*When you app runs a Query , CosmosDB normally deserializes the result stream from DB, 
		 turns it into a resource object and then serializes it back into JSON for the application.
		interms of CPU costs, its not free. so SDK provides streaming version of each iterator as higher performance alternative.

		Streaming iterator eliminates extra overhead on CosmosDB side and returns raw strean directly to your application intead.
		given the strean , you can deserialize it yourself to process the results or pass to downsteam app directly
		 */
        private async static Task QueryWithStatefulPagingStreamed()
        {
			Console.Clear();
			Console.WriteLine(">>> Query Documents with Streaming <<<");
            Console.WriteLine();

            var container = Shared.Client.GetContainer("mydb", "mystore");
            var sql = "SELECT * FROM c";

            // Get all pages of large resultset using iterator.HasMoreResults
            Console.WriteLine("Querying for all documents (full resultset, stateful, w/streaming iterator)");
            var streamIterator = container.GetItemQueryStreamIterator(sql);
            var itemCount = 0;
            var pageCount = 0;
            while (streamIterator.HasMoreResults)
            {
                pageCount++;
                var results = await streamIterator.ReadNextAsync();//return raw stream
                var stream = results.Content;
                using (var sr = new StreamReader(stream))// use streamreader to read stream
                {
                    var json = await sr.ReadToEndAsync();
                    var jobj = JsonConvert.DeserializeObject<JObject>(json);
                    var jarr = (JArray)jobj["Documents"];
                    foreach (var item in jarr)
                    {
                        var customer = JsonConvert.DeserializeObject<Customer>(item.ToString());
                        Console.WriteLine($" ({pageCount}.{++itemCount}) Id: {customer.Id}; Name: {customer.Name};");
                    }
                }
            }
            Console.WriteLine($"Retrieved {itemCount} documents (full resultset, stateful, w/streaming iterator");
            Console.WriteLine();
        }

        private async static Task QueryWithStatelessPagingStreamed()
        {
            // Get all pages of large resultset using continuation token
            Console.WriteLine("Querying for all documents (full resultset, stateless, w/streaming iterator)");

            var continuationToken = default(string);
            do
            {
                continuationToken = await QueryFetchNextPageStreamed(continuationToken);
            } while (continuationToken != null);//simulate client call for each page

            Console.WriteLine($"Retrieved all documents (full resultset, stateless, w/streaming iterator)");
            Console.WriteLine();
        }

        private async static Task<string> QueryFetchNextPageStreamed(string continuationToken)
        {
            var container = Shared.Client.GetContainer("mydb", "mystore");
            var sql = "SELECT * FROM c";

            var streamIterator = container.GetItemQueryStreamIterator(sql, continuationToken);
            var response = await streamIterator.ReadNextAsync();

            var itemCount = 0;

            if (continuationToken != null)
            {
                Console.WriteLine($"... resuming with continuation {continuationToken}");
            }

            var stream = response.Content;
            using (var sr = new StreamReader(stream))
            {
                var json = await sr.ReadToEndAsync();
                var jobj = JsonConvert.DeserializeObject<JObject>(json);
                var jarr = (JArray)jobj["Documents"];
                foreach (var item in jarr)
                {
                    var customer = JsonConvert.DeserializeObject<Customer>(item.ToString());
                    Console.WriteLine($" ({++itemCount}) Id: {customer.Id}; Name: {customer.Name};");
                }
            }

            continuationToken = response.Headers.ContinuationToken;

            if (continuationToken == null)
            {
                Console.WriteLine($"... no more continuation; resultset complete");
            }

            return continuationToken;
        }

		private static void QueryWithLinq()
		{
			Console.Clear();
			Console.WriteLine(">>> Query Documents (LINQ) <<<");
			Console.WriteLine();

			Console.WriteLine("Querying for UK customers (LINQ)");
			var container = Shared.Client.GetContainer("mydb", "mystore");

			//SDK converts LINQ query to CosmosDB SQL
			var q = from d in container.GetItemLinqQueryable<Customer>(allowSynchronousQueryExecution: true)
					where d.Address.CountryRegionName == "United Kingdom"
					select new
					{
						d.Id,
						d.Name,
						d.Address.Location.City
					};
			//SELECT VALUE ....FROM..WHERE..

			var documents = q.ToList();

			Console.WriteLine($"Found {documents.Count} UK customers");
			foreach (var document in documents)
			{
				var d = document as dynamic;
				Console.WriteLine($" Id: {d.Id}; Name: {d.Name}; City: {d.City}");
			}
			Console.WriteLine();
		}

		//you don't need to delete and recreate document to replace
		//you can directly update document inplace
		//but there is exception is > if you need to change the partition key as part of migration. since partiton key is immutable that can only be done by deleting and recreating the document
		private async static Task ReplaceDocuments()
		{
			Console.Clear();
			Console.WriteLine(">>> Replace Documents <<<");
			Console.WriteLine();

			var container = Shared.Client.GetContainer("mydb", "mystore");//"adventure-works","stores"

			Console.WriteLine("Querying for documents with 'isNew' flag");
			var sql = "SELECT VALUE COUNT(c) FROM c WHERE c.isNew = true";
			var count = (await (container.GetItemQueryIterator<int>(sql)).ReadNextAsync()).First();
			Console.WriteLine($"Documents with 'isNew' flag: {count}");
			Console.WriteLine();

			Console.WriteLine("Querying for documents to be updated");
			sql = "SELECT * FROM c WHERE STARTSWITH(c.name, 'New Customer') = true";
			var documents = (await (container.GetItemQueryIterator<dynamic>(sql)).ReadNextAsync()).ToList();
			Console.WriteLine($"Found {documents.Count} documents to be updated");
			foreach (var document in documents)
			{
				document.isNew = true;
				var result = await container.ReplaceItemAsync<dynamic>(document, (string)document.id);
				var updatedDocument = result.Resource;
				Console.WriteLine($"Updated document 'isNew' flag: {updatedDocument.isNew}");
			}
			Console.WriteLine();

			Console.WriteLine("Querying for documents with 'isNew' flag");
			sql = "SELECT VALUE COUNT(c) FROM c WHERE c.isNew = true";
			count = (await (container.GetItemQueryIterator<int>(sql)).ReadNextAsync()).First();
			Console.WriteLine($"Documents with 'isNew' flag: {count}");
			Console.WriteLine();
		}

		private async static Task DeleteDocuments()
		{
			Console.Clear();
			Console.WriteLine(">>> Delete Documents <<<");
			Console.WriteLine();

			var container = Shared.Client.GetContainer("mydb", "mystore");

			Console.WriteLine("Querying for documents to be deleted");
			var sql = "SELECT c.id, c.address.postalCode FROM c WHERE STARTSWITH(c.name, 'New Customer') = true";
			var iterator = container.GetItemQueryIterator<dynamic>(sql);
			var documents = (await iterator.ReadNextAsync()).ToList();
			Console.WriteLine($"Found {documents.Count} documents to be deleted");
			foreach (var document in documents)
			{
				string id = document.id;
				string pk = document.postalCode;
				await container.DeleteItemAsync<dynamic>(id, new PartitionKey(pk));
			}
			Console.WriteLine($"Deleted {documents.Count} new customer documents");
			Console.WriteLine();
		}

	}
}

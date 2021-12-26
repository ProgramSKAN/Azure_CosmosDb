# Azure_CosmosDb

### Concepts of cosmosdb

- Introduction
- Throughput and cost
- Horizantal Partitioning
- Global Distribution

### How-to

- Data modeling and migration
- querying with SQL
- programming with .net SDK

* Building cosmosdb applications (serverside progeamming,batch & bulk operations , change feed)
* alternative way of modeling data in cosmosdb
  - Table API > keyvalue store
  - Gramlin API >graph db

# NoSQL ?

- to solve problem of Big data. ie, 3 Vs
  - volume > db size Tb,Pb,..
  - velocity >Throughput , keep pace with requests that coming in fast (distributed workload across machines )
  - variety > this is about schema and problems that occur in relational DB regarding schema changes. as schemas evolve more, it is difficult to apply those schema changes to mission critical databases that require continuous uptime around clock.this is even more difficult for distributed DBs when those schema changes need to be deployed to multiple regions.

* Relation Dbs can't handle Big data
* in NoSql instead of one big machine (Tb,Pb size), scale-out to multiple machines in the face of volume , velocity and variety

# What is NoSql DB?

- Distributed :: Replicas ensure high availability and resilience
  - NoSql db is ditributed from groundup.no extra effort needed.
  * multiple copies of data are distributed to separate replicas. more copies you have of same data , more available it is to more users. so, more resilient to failure if and when replica should go down.

* Scale-out :: Horizantal Partitoning for elastic storage and throughput.

  - horizontal partitioning, which balances the workload uniformly across a cluster of machines.these capabilities are built in.

* Schema-free :: no enforced schema
  - it doesn't mean there isn't any schema, but no enforced schema. So, every item in database have schema, which is its shape, but that shape can change without friction and downtime unlike relational databases and enforced schemas.

# Cosmos DB

- Cosmos DB is massively scalable NoSQL database that's available to your application as a fully managed Platform as a Service that runs on Azure.
- There are comprehensive service level agreements on **throughput, latency, consistency, and availability**, so you're guaranteed no more than 52 minutes of downtime a year with four 9s of availability, or no more than 5 minutes of downtime a year with five 9s of availability, which you can get if you globally distribute your data.

* it delivers blazing fast reads and writes, typically within 10 ms or less,
* whether you'v GB, TB or PB of data. Cosmos DB automatically manages the growth of your data by horizontally partitioning the workload behind the scenes.

* The process is completely transparent and provides the elastic scale for virtually unlimited storage to handle volume, as well as throughput for velocity. Like all Azure services, Cosmos DB runs in many Microsoft data centers throughout the world. Within any one data center, Cosmos DB replicates your data automatically, and that ensures high availability within that one region. But, you can also replicate your data across multiple data centers and distribute your data globally. Simply point and click on a map, and Cosmos DB does the rest, bringing your data closer to your consumers wherever they are. You can also enable multi‑master, and that will ensure high performance writes, as well as reads in any region where your data is distributed. And Cosmos DB has multiple APIs that support a variety of schema‑free data models. This includes not only JSON documents using either the SQL API or the MongoDB API, but table, graph, and columnar data models as well, which are exposed by the Table, Gremlin, and Cassandra APIs, respectively.

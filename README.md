# Polybus

Polybus is a lightweight language-agnostic Event Bus for communication between microservice in asynchronous mode. Even though this repository is C# but the concept can be apply to other languages. Thus, enabled service communication written in different language.

## Using

First, install the implementation for the message broker you want to use:

- [RabbitMQ](https://www.nuget.org/packages/Polybus.RabbitMQ/)

It is recommended to integrate [Grpc.Tools](https://github.com/grpc/grpc/blob/master/src/csharp/BUILD-INTEGRATION.md) to the project to enable automatic compilation of `.proto` files by adding the following to `.csproj`:

```xml
  <ItemGroup>
    <PackageReference Include="Grpc.Tools" Version="2.32.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <Protobuf Include="**/*.proto" GrpcServices="None"/>
  </ItemGroup>
```

### Publishing event

You need to construct an instance of `IEventPublisher` implementation, which is depend on the message broker you use (e.g. `Polybus.RabbitMQ.EventPublisher` for RabbitMQ). If you are using Microsoft Dependency Injection framework you can use extension method of `IServiceCollection` provided by each implementation to register `IEventPublisher` automatically. e.g.:

```csharp
services.AddRabbitMQConnection(new ConnectionFactory()
{
    DispatchConsumersAsync = true, // This field is required.
    // Populate other connection information.
});

services.ConfigureRabbitMQEventBus(options =>
{
    // Populate properties of options.
});

services.AddRabbitMQPublisher();
```

## Development

### Prerequisites

- .NET Core 3.1
- Protocol Buffer Compiler
- Docker Compose

### Build

```sh
dotnet build src/Polybus.sln
```

### Running tests

First you need to start the required services with Docker Compose:

```sh
docker-compose up -d
```

Then run all tests with:

```sh
dotnet test src/Polybus.sln
```

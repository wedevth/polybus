# Polybus

Polybus is a lightweight language-agnostic Event Bus for communication between microservice in asynchronous mode. Even though this repository is C# but the concept can be apply to other languages. Thus, enabled service communication written in different language.

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

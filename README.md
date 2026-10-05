# English Learning - Test Service

Independent microservice repository managing test/quiz generation, multiple-choice questions with AI-generated distractors, session submission, scoring, and Kafka progress events.

## Architecture & Responsibilities
- **Domain**: Test session aggregates, questions, user answers, scoring rules.
- **Application**: Test creation orchestrating candidate words from Learning Service and distractor generation from AI Service.
- **Infrastructure**: Entity Framework Core with SQL Server, transactional Outbox pattern for publishing test completed events to Kafka, and gRPC client implementations for Learning Service and AI Service.
- **Api**: REST APIs on port 5005 with JWT authentication and OpenTelemetry metrics.

## Getting Started

### Prerequisites
- .NET 10 SDK
- SQL Server
- Kafka broker
- Learning Service (gRPC) & AI Service (gRPC)

### Build, Test & Run
```bash
# Restore & build
dotnet restore TestService.slnx
dotnet build TestService.slnx

# Run tests
dotnet test TestService.slnx

# Run API
dotnet run --project src/Test.Api
```

### Docker
```bash
docker build -t english-learning-test:latest .
docker run -p 5005:5005 english-learning-test:latest
```

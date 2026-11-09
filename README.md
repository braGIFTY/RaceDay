RaceDay

RaceDay is a .NET 8 event management platform for South Africa’s road running, walking, and cycling community. Organisers can create and manage events, define categories, view enrolments, and record results. Participants can browse events, enter by selecting a category, and track their enrolments and results.

This repository contains all three parts of the project:
- Part 1 — System planning and database: ERD, API endpoint plan, SQL script, and supporting design decisions. See docs/.
- Part 2 — RESTful API: ASP.NET Core Web API with Entity Framework Core, session-based authentication, Swagger, unit tests, and CI. See RaceDay.Api/ and RaceDay.Api.Tests/.
- Part 3 — MVC front end and cloud storage: ASP.NET Core MVC application that consumes the API, Azure Blob Storage for event banners and profile pictures, and Docker containerisation for the MVC app.
Roles and features
Organisers
- Register, log in, and log out
- View an organiser dashboard
- Create, edit, and delete events
- Upload event banner images
- Manage event categories
- View event enrolments
- Record participant results
Participants
- Register, log in, and log out
- Browse and filter upcoming events
- View event details
- Enter events by selecting a category
- View and cancel enrolments
- View race results and finishing positions
- Update their profile and upload a profile picture
Authentication and authorisation
The API enforces role-based access and ownership checks. The MVC application uses session-based authentication, separates Organiser and Participant navigation, protects role-specific pages, and redirects users when their API session expires.
Repository structure
RaceDay/
├── .github/workflows/              # GitHub Actions workflows
├── docs/                           # Part 1 documents, scripts, and CI screenshots
├── RaceDay.Api/                    # ASP.NET Core Web API
│   ├── Controllers/
│   ├── Data/
│   ├── DTOs/
│   ├── Filters/
│   ├── Migrations/
│   ├── Models/
│   └── Program.cs
├── RaceDay.Api.Tests/              # API unit tests
├── RaceDay.Mvc/                    # ASP.NET Core MVC application (Part 3)
├── README.md
└── ...
Prerequisites
- .NET 8 SDK
- SQL Server (SQL Server Express is suitable)
- Visual Studio 2022 or later with the ASP.NET and web development workload (optional)
- Docker Desktop (to build and run the MVC container)
- An Azure Storage account (for image uploads)
Get the code
git clone https://github.com/braGIFTY/RaceDay.git
cd RaceDay
Configure the database
Check the connection string in RaceDay.Api/appsettings.json and update it to match your SQL Server instance:

"ConnectionStrings": {
"RaceDayDb": "Server=.\SQLEXPRESS;Database=RaceDayDB;Trusted_Connection=True;TrustServerCertificate=True;"
}

Create the database from the EF Core migrations.

Visual Studio: Open the solution, select RaceDay.Api as the default project in Package Manager Console, and run:

Update-Database

Command line:

dotnet tool install --global dotnet-ef --version 8.*
cd RaceDay.Api
dotnet ef database update

This creates the database tables. The migration-built database starts empty.

Note: docs/raceday-schema.sql is the Part 1 planning script. It drops and recreates tables, so do not run it against the database created by the Part 2 migrations.
Configure Azure Blob Storage
The API uses Azure Blob Storage for event banner and participant profile images. Store the settings in User Secrets for the API project:

cd RaceDay.Api
dotnet user-secrets init
dotnet user-secrets set "AzureStorage:ConnectionString" "YOUR_CONNECTION_STRING"
dotnet user-secrets set "AzureStorage:ContainerName" "YOUR_CONTAINER_NAME"

The equivalent configuration is:

{
"AzureStorage": {
"ConnectionString": "YOUR_CONNECTION_STRING",
"ContainerName": "YOUR_CONTAINER_NAME"
}
}

Uploaded images are stored in the configured container, and their URLs are saved in the database.
Run the API
From the repository root:

dotnet run --project RaceDay.Api --launch-profile https

Or set RaceDay.Api as the startup project in Visual Studio and run it. Swagger is available at the local URL printed in the console, typically https://localhost:7146/swagger.

If the development certificate is not trusted, run:

dotnet dev-certs https --trust
Run the MVC application
Make sure the API is running, then configure the API base URL in the MVC application’s appsettings.json or through configuration for your environment.

Run from the repository root:

dotnet run --project RaceDay.Mvc

Or set the MVC project as the startup project in Visual Studio.
Run the MVC application with Docker
Build the MVC image from the repository root:

docker build -t raceday-mvc -f RaceDay.Mvc/Dockerfile .

Run the container, replacing the API URL with the address reachable from the container:

docker run --rm -p 8080:8080 
-e Api__BaseUrl=http://host.docker.internal:YOUR_API_PORT 
raceday-mvc

The MVC Docker image uses a multi-stage build, runs as a non-root user, and accepts configuration through environment variables.
Try the API in Swagger
Swagger shares a session cookie within the browser. Logging in as another user replaces the current session, so use a private or incognito window when testing separate accounts.
1. Register and log in as an organiser using POST /api/auth/register/organiser and POST /api/auth/login.
2. Create an event with POST /api/events, then add a category with POST /api/events/{eventId}/categories.
3. Register and log in as a participant. This replaces the organiser’s session.
4. Enrol in the event with POST /api/enrolments, providing the event and category IDs.
5. Log in as the organiser again, view enrolments with GET /api/events/{eventId}/enrolments, and record a result with POST /api/enrolments/{id}/results.
6. Log in as the participant and view their results with GET /api/results/me.
The API endpoints, request bodies, and response codes are documented in Swagger and listed in docs/endpoint-plan.md.
Authentication and role-based access
- Registration uses separate endpoints for organisers and participants. Passwords must be at least eight characters and are stored as salted hashes.
- Login stores the user ID and role in a server-side session. The browser holds an HttpOnly session cookie. Sessions expire after 30 minutes of inactivity and are held in memory, so restarting the API logs users out.
- [RequireAuth] checks for an active session. [RequireRole("Organiser")] and [RequireRole("Participant")] restrict access by role.
- Event and category browsing is public. Other API endpoints require an active session.
- Organisers can only manage their own events, categories, and results. Participants can only cancel their own enrolments.
- The API checks both account tables to prevent an email address from being used by both an Organiser and a Participant.
Run the unit tests
There are 89 API unit tests.

Visual Studio: Use Test → Test Explorer → Run All Tests.

Command line:

dotnet test RaceDay.Api.Tests/RaceDay.Api.Tests.csproj

The tests run the API in memory and use EF Core’s in-memory database, so SQL Server is not required to run them.
CI/CD
GitHub Actions workflows run on pushes to main:
- validate-docs.yml: checks that the Part 1 documentation files are present.
- build-and-test.yml: restores dependencies, builds the API and test project, and runs the API tests.
The Part 3 workflow can be documented here once its final workflow name and build steps are confirmed.
CI screenshots
Add or update screenshots in docs/ as the workflows are completed:
- Part 1 CI: docs/ci-success.png
- Part 2 build: docs/part2-ci-build.png
- Part 2 tests: docs/part2-ci-tests.png

Part 1 planning and database
Part 1 is the specification used to build the API. The ERD, endpoint plan, SQL script, and planning document are in docs/. The full write-up is docs/RaceDay System Planning and Database.docx.

To inspect the Part 1 database independently, run docs/raceday-schema.sql in SQL Server Management Studio on a database instance where you do not need to preserve the Part 2 migration-built database. The script drops and recreates the tables.
Video walkthroughs
- Part 1 video walkthrough: https://youtu.be/US8X2dPBVJo
- Part 2 video walkthrough: https://youtu.be/eBEc5hjMr5M
- Part 3 video walkthrough: https://youtu.be/8_c7FUskuM0 
Technologies
- ASP.NET Core Web API and MVC (.NET 8)
- Entity Framework Core
- SQL Server
- Azure Blob Storage
- Docker
- GitHub Actions
- Bootstrap 5
- xUnit


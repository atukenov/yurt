# Yurt Project Setup

This document describes how to install dependencies and run the full project locally.

## 1. Prerequisites

Install these tools first:

- `.NET 8 SDK` or newer
  - Verify: `dotnet --version`
- `SQL Server` or `LocalDB`
  - LocalDB is a good development option if you have Visual Studio installed
- `Node.js 18+`
  - Verify: `node --version`
  - `npm --version`
- `Angular CLI 21`
  - Install: `npm install -g @angular/cli@21`

## 2. Backend setup

### 2.1 Configure the database

Open `backend/src/Yurt.WebApi/appsettings.json` and set a valid connection string under `ConnectionStrings.DefaultConnection`.

Recommended development values:

- LocalDB:
  ```json
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=YurtDb;Trusted_Connection=True;TrustServerCertificate=True;"
  ```
- SQL Server Express:
  ```json
  "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=YurtDb;Trusted_Connection=True;TrustServerCertificate=True;"
  ```
- Docker SQL Server:
  ```json
  "DefaultConnection": "Server=localhost;Database=YurtDb;User Id=sa;Password=YourStrongPassword123;TrustServerCertificate=True;"
  ```

### 2.2 Set the JWT secret

In the same file, replace the placeholder secret:

```json
"Secret": "CHANGE_THIS_TO_A_STRONG_SECRET_KEY_AT_LEAST_32_CHARS"
```

Use a strong random value with at least 32 characters.

### 2.3 Restore and apply migrations

From the repository root:

```bash
cd backend

dotnet restore

dotnet ef database update --project src/Yurt.Infrastructure --startup-project src/Yurt.WebApi
```

If `dotnet ef` is not installed, run:

```bash
dotnet tool install --global dotnet-ef
```

### 2.4 Run the API

Start the backend from `backend`:

```bash
dotnet run --project src/Yurt.WebApi
```

The API should start on `http://localhost:5000` and Swagger will be available at `http://localhost:5000/swagger` when running in Development mode.

## 3. Frontend setup

### 3.1 Install dependencies

From the repository root:

```bash
cd frontend
npm install
```

### 3.2 Run the customer app

```bash
cd frontend
ng serve yurt-customer --port 4200
```

Open: `http://localhost:4200`

### 3.3 Run the admin app

```bash
cd frontend
ng serve yurt-admin --port 4300
```

Open: `http://localhost:4300`

## 4. Useful commands

### Backend

```bash
cd backend

dotnet restore

dotnet ef database update --project src/Yurt.Infrastructure --startup-project src/Yurt.WebApi
dotnet run --project src/Yurt.WebApi
```

### Frontend

```bash
cd frontend
npm install
ng serve yurt-customer --port 4200
ng serve yurt-admin --port 4300
```

### Build production artifacts

```bash
cd frontend
ng build yurt-customer --configuration production
ng build yurt-admin --configuration production
```

## 5. Notes

- The backend allows origins `http://localhost:4200` and `http://localhost:4300` by default.
- If you deploy to a remote host, update `AllowedOrigins` in `backend/src/Yurt.WebApi/appsettings.json`.
- If the backend is hosted remotely, point the frontend to the deployed API URL.
- If `dotnet --version` fails, install the .NET 8 SDK before continuing.

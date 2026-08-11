# ANpay - Digital Wallet System

A modern digital wallet platform built with ASP.NET 10 Web API and Blazor Server.

## Features

- **User Authentication** - JWT-based secure login/register
- **Role-Based Access Control** - SuperAdmin, BranchAdmin, Official, Customer
- **Multi-Wallet Support** - Create multiple wallets with different currencies
- **Money Transfer** - Send money between wallets instantly
- **Transaction History** - Complete transaction tracking with status
- **Branch Management** - Manage multiple branches and staff

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Backend | ASP.NET 10 Web API |
| Database | SQL Server (SSMS) |
| ORM | Entity Framework Core 10 |
| Auth | ASP.NET Identity + JWT |
| Frontend | Blazor Server (planned) |

## Project Structure

```
ANpay/
├── Backend/                 # ASP.NET 10 Web API
│   ├── Controllers/         # API Controllers
│   ├── Models/              # Database Models
│   ├── DTOs/                # Data Transfer Objects
│   ├── Services/            # Business Logic
│   ├── Data/                # DbContext & Migrations
│   └── Program.cs           # App Configuration
├── Mobile/                  # .NET MAUI (planned)
└── WebDashboard/            # Blazor Admin Panel (planned)
```

## API Endpoints

### Authentication
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/register` | Register new user |
| POST | `/api/auth/login` | Login (get JWT token) |
| GET | `/api/auth/profile` | Get user profile |

### Wallet
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/wallet` | Create wallet |
| GET | `/api/wallet` | Get all user wallets |
| GET | `/api/wallet/{id}` | Get wallet by ID |
| POST | `/api/wallet/deposit` | Deposit money |
| POST | `/api/wallet/withdraw` | Withdraw money |
| POST | `/api/wallet/transfer` | Transfer between wallets |

### Transaction
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/transaction/wallet/{id}` | Get transaction history |
| GET | `/api/transaction/{id}` | Get transaction details |

## Roles

| Role | Permissions |
|------|------------|
| **SuperAdmin** | Full access: manage all users, branches, transactions |
| **BranchAdmin** | Manage branch officials, view branch transactions |
| **Official** | Process customer transactions |
| **Customer** | Own wallet operations only |

## Getting Started

### Prerequisites
- .NET 10 SDK
- SQL Server (SSMS)

### Setup

1. Clone the repository:
```bash
git clone https://github.com/TheRealLaxman/ANpay.git
cd ANpay/Backend
```

2. Update connection string in `appsettings.json`:
```json
"DefaultConnection": "Server=YOUR_SERVER;Database=ANpayDB;Trusted_Connection=True;"
```

3. Apply database migration:
```bash
dotnet ef database update
```

4. Run the API:
```bash
dotnet run
```

5. Open Swagger at `https://localhost:7254/swagger`

## Default Admin Account

- **Email:** admin@anpay.com
- **Password:** Admin@123
- **Role:** SuperAdmin

## License

Private - All rights reserved.

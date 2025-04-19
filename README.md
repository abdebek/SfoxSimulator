# sFoxHub Simulator

A lightweight .NET Core SignalR-based emulator that simulates real-time market data feeds similar to sFox. It supports public feeds like `ticker`, `trades`, and `orderbook` without the authentication logic.

## ✨ Features

- Simulates live market data streams (ticker, trades, orderbook)
- WebSocket-based via SignalR
- Randomized but structured market data
- Supports multiple concurrent client connections
- Automatic cleanup of unused feeds

## 🛠️ Tech Stack

- ASP.NET Core
- SignalR
- System.Reactive for simulation flow
- C# 10 / .NET 6+

## 🚀 Running the Project

1. **Clone the repo:**

   ```bash
   git clone https://github.com/abdebek/sfoxhub-emulator.git
   cd sfoxhub-emulator


- Backend - ./SfoxSimulator

- Frontend - ./WebClient

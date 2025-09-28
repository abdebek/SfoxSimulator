# sfoxFeedLibrary - sFoxHub

**sFoxHub** is a lightweight SignalR-based market data emulator inspired by platforms like sFox. It provides a real-time feed of order book updates and trade ticks, designed for development, testing, or demo purposes.

## Features

- 📡 Real-time broadcasting with SignalR
- 🔁 Emulated order book and trade data
- 🛠️ Plug-and-play as a hosted service
- 🧪 Ideal for testing market data clients

## Usage

1. Add the hosted service to your DI container:

```csharp
// Register the feed service as a singleton
builder.Services.AddSfoxSimulatorHostedService();
```

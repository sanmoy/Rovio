# Rovio Matchmaking Service - Coding Challenge

Custom matchmaking service designed and developed using C# and .NET 10.0 for a Rovio Server Developer role.

## Architecture & Project Structure

The solution consists of the following projects:

1. **`Matchmaking.Shared`**: A shared library containing shared DTOs, data models, and enums (e.g. `MatchmakingTicket`, `PlayerLatency`, `TicketStatus`, `MatchSession`). It ensures a DRY codebase and eliminates JSON contract mismatches across microservices.
2. **`Matchmaking.Api`**: The client-facing REST API hosting public endpoints for ticketing:
   - `POST /games/{gameId}/matchmaking/tickets`: Submit a ticket request
   - `GET /games/{gameId}/matchmaking/tickets/{ticketId}`: Poll matchmaking ticket status
   - `DELETE /games/{gameId}/matchmaking/tickets/{ticketId}`: Cancel ticket (remove from queue)
3. **`Matchmaking.Storage`**: A REST wrapper simulating a Redis database layer. It manages tickets in an in-memory thread-safe storage (`ConcurrentDictionary`).
4. **`Matchmaking.Worker`**: The core matchmaking worker that runs periodic matching loops, groups players based on region latency and queue wait times, and communicates with the mock game server to spawn sessions.
5. **`Matchmaking.MockGameServer`**: A mock game server simulating game session allocation, capacity management, and registration.
6. **`Matchmaking.Tests`**: Unit tests verifying the matchmaking logic, ticketing API logic, and queue priorities.

---

## How to Run

### Option 1: Visual Studio / JetBrains Rider (IDE)
1. Open `MatchmakingService.sln`.
2. Right-click the solution in the Solution Explorer and select **Configure Startup Projects...**
3. Select **Multiple startup projects**.
4. Set the following projects to **Start**:
   - `Matchmaking.Storage` (Port 5010)
   - `Matchmaking.Api` (Port 5000)
   - `Matchmaking.Worker`
   - `Matchmaking.MockGameServer` (Port 5020)
5. Press `F5` / **Start Debugging**. This will launch all four processes concurrently.

### Option 2: Command Line (CLI Scripts)
You can run the startup scripts located at the repository root:

- **Windows (PowerShell)**:
  ```powershell
  ./run.ps1
  ```
- **macOS / Linux (Bash)**:
  ```bash
  chmod +x run.sh
  ./run.sh
  ```

These scripts start `Matchmaking.Storage` first, wait for it to be ready, and then launch the remaining services in the background or new windows.

---

## Running Unit Tests

To run the unit test suite, execute the following command:
```bash
dotnet test
```

---

## Matchmaking Algorithm & Scaling

### Matching Strategy
The `Matchmaking.Worker` runs a periodic matchmaking sweep loop that operates on active partitions defined by `(gameId, region)`:
1. **Oldest-First Processing**: Candidates are processed in ascending order of wait time to ensure queue fairness and prevent starvation.
2. **Dynamic Latency Widening**: Every player starts with a strict latency tolerance (base value of `30ms`). As a player waits in queue, their allowable latency deviation grows by `10ms` every `5 seconds`, capped at a maximum of `200ms`.
3. **Latency Consistency**: Two players are matched only if the absolute difference in their reported latencies to the region is within the threshold:
   $$\text{abs}(\text{playerA.latency} - \text{playerB.latency}) \le \text{threshold}$$
4. **Backfilling (Optional)**: If a player cannot form a new group, the system searches for an active game session in that region with open slots and checks if the player's latency matches the session's reference latency.

### Known Scaling Constraints & Optimization
- **Sweep Complexity**: Currently, the matching sweep checks each ticket against all other partition candidates, yielding an $O(n^2)$ complexity per partition.
- **Production Improvement**: In a production-grade deployment with millions of players, this all-pairs comparison would become a bottleneck. We would optimize this by pre-sorting/bucketing players into coarse latency groups (e.g. 10ms bins). The sweep would then run matches within adjacent bins rather than comparing every ticket globally.
- **Distributed Worker Scaling & Partition Leasing**: For complete architecture details on scaling to millions of users with distributed workers, partition leasing, and zero double-booking, see the dedicated [Scaling Architectural Blueprint](SCALING_NOTE.md).

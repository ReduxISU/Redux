# Quantum API Client for Redux

## Overview

The `QuantumServerAPI` class provides a reusable HTTP client for making POST requests to quantum computing API servers with easy server switching.

## Location
- **Main Client**: `/Tools/QuantumServerAPI.cs`
- **Deutsch Implementation**: `/Problems/NPComplete/NPC_DEUTSCH/Solvers/DeutschQuantumSolver.cs`
- **Deutsch Visualization**: `/Problems/NPComplete/NPC_DEUTSCH/Visualizations/DeutschDefaultVisualization.cs`

## Features

✅ Switch between servers in appsettings.json
✅ Reusable POST methods with JSON serialization
✅ Support for typed and untyped responses
✅ Automatic error handling and timeouts
✅ Simple, clean API
✅ Integration with quantum algorithm solvers and visualizations

## Basic Usage

### 1. Simple POST Request

```csharp
using API.Tools;

// Create client (defaults to ISU_AWS)
var client = new QuantumServerAPI();

// Prepare body (boolean array for Deutsch)
bool[] body = new[] { true, false };

// Make POST request
string response = await client.PostAsync("/deutsch-quantum", body);
```

### 2. Typed Response

```csharp
// Define response type
public class DeutschQuantumResponse
{
    public string answer { get; set; }
    public string qasm { get; set; }
}

// Make request with typed response
var client = new QuantumServerAPI();
DeutschQuantumResponse? result = await client.PostAsync<DeutschQuantumResponse>("/deutsch-quantum", body);

if (result != null)
{
    Console.WriteLine($"Answer: {result.answer}");
    Console.WriteLine($"QASM: {result.qasm}");
}
```

### 3. Raw JSON String

```csharp
var client = new QuantumServerAPI();
string jsonBody = "[true, false]";
string response = await client.PostRawJsonAsync("/deutsch-quantum", jsonBody);
```

## Using in DeutschQuantumSolver

The `DeutschQuantumSolver` class uses the Quantum API:

```csharp
// Default constructor
var solver = new DeutschQuantumSolver();

// Or specify environment
var localSolver = new DeutschQuantumSolver();

// Use it
DEUTSCH problem = new DEUTSCH("(0,1)");
string result = solver.solve(problem);  // Returns "constant" or "balanced"
```

### How DeutschQuantumSolver Works

1. Extracts `funcValues` from the DEUTSCH problem instance
2. Calls the quantum API with the function values as a boolean array
3. Parses the JSON response to extract just the `answer` field
4. Returns "constant" or "balanced"

### How DeutschDefaultVisualization Works

1. Receives the answer from the solver
2. Makes its own call to the quantum API with the problem's `funcValues`
3. Extracts both the `answer` and `qasm` fields from the response
4. Returns a quantum circuit visualization with the QASM code

## Testing via Redux API

### Using Swagger UI

1. Start the API: `dotnet run` or `./buildAndRun.sh`
2. Open: `http://0.0.0.0:27000/swagger`
3. Test the solver:
   - Endpoint: `POST /ProblemProvider/solve`
   - Parameters:
     - `solver`: `"DeutschQuantumSolver"`
     - `problemInstance`: `"(0,1)"`
   - Click **Execute**
4. Test the visualization:
   - Endpoint: `GET /ProblemProvider/visualize`
   - Parameters:
     - `visualization`: `"DeutschDefaultVisualization"`
     - `solver`: `"DeutschQuantumSolver"`
     - `problemInstance`: `"(0,1)"`
   - Click **Execute**

### Using curl

```bash
# Test the solver - returns just the answer
curl -X POST "http://0.0.0.0:27000/ProblemProvider/solve?solver=DeutschQuantumSolver" \
  -H "Content-Type: application/json" \
  -d '"(0,1)"'

# Expected response: "constant" or "balanced"

# Test the visualization - returns answer + QASM circuit
curl -X GET "http://0.0.0.0:27000/ProblemProvider/visualize?visualization=DeutschDefaultVisualization&solver=DeutschQuantumSolver&problemInstance=(0,1)"

# Expected response: JSON with circuit and solution fields
```

### Testing the Quantum API Directly

```bash
# Test the quantum endpoint directly
curl -X POST http://127.0.0.1:5000/deutsch-quantum \
  -H "Content-Type: application/json" \
  -d '[true, false]'

# Expected response:
# {
#   "answer": "constant",
#   "qasm": "OPENQASM 2.0;\ninclude \"qelib1.inc\";\n..."
# }
```

## Configuration

### Timeouts

Default timeout is **30 seconds**. To change, modify in `QuantumServerAPI.cs`:

```csharp
_httpClient = new HttpClient
{
    BaseAddress = new Uri(_baseUrl),
    Timeout = TimeSpan.FromSeconds(60)  // Change from 30 to 60
};
```

## Error Handling

The client throws exceptions for:
- **HttpRequestException**: Network errors, HTTP errors (4xx, 5xx)
- **TimeoutException**: Request timeout (default 30s)
- **JsonException**: JSON serialization/deserialization errors

Example error handling in solver:

```csharp
public string solve(DEUTSCH problem)
{
    try
    {
        bool[] requestBody = problem.funcValues;
        var client = new QuantumServerAPI();
        string response = client.PostAsync("/deutsch-quantum", requestBody).Result;

        // Parse and return answer
        using JsonDocument doc = JsonDocument.Parse(response);
        JsonElement root = doc.RootElement;

        if (root.TryGetProperty("answer", out JsonElement answerElement))
        {
            return answerElement.GetString() ?? "No answer found";
        }

        return response;
    }
    catch (Exception ex)
    {
        return $"{{\"error\": \"{ex.Message}\"}}";
    }
}
```

## Response Format

The quantum API returns JSON with two fields:

### Deutsch Quantum Response

```json
{
  "answer": "constant",
  "qasm": "OPENQASM 2.0;\ninclude \"qelib1.inc\";\nqreg q[2];\ncreg c[1];\nx q[1];\nh q[0];\nh q[1];\nh q[0];\nmeasure q[0] -> c[0];"
}
```

**Fields:**
- `answer`: Either "constant" or "balanced" - the result of Deutsch's algorithm
- `qasm`: OpenQASM 2.0 quantum circuit code representing the computation

## Problem Instance Format

For the Deutsch problem, instances are formatted as `(i, w)`:
- `i`: Integer (0 or 1) - first function value
- `w`: Integer (0 or 1) - second function value

These get converted to `funcValues` boolean array in the DEUTSCH class:
- `(0, 0)` → `[false, false]`
- `(0, 1)` → `[true, false]`
- `(1, 0)` → `[false, true]`
- `(1, 1)` → `[true, true]`

## API Endpoints

The quantum server should provide:

### `/deutsch-quantum` (POST)
- **Input**: Boolean array `[bool, bool]`
- **Output**: JSON with `answer` and `qasm` fields
- **Purpose**: Run Deutsch's algorithm on a quantum computer/simulator

Example:
```bash
POST /deutsch-quantum
Content-Type: application/json

[true, false]
```

## Files

1. **`/Tools/QuantumServerAPI.cs`** - Main HTTP client class
2. **`/Problems/NPComplete/NPC_DEUTSCH/Solvers/DeutschQuantumSolver.cs`** - Deutsch quantum solver
3. **`/Problems/NPComplete/NPC_DEUTSCH/Visualizations/DeutschDefaultVisualization.cs`** - Deutsch visualization
4. **`/Tools/QUANTUM_API_README.md`** - This documentation

## Architecture

```
Redux API Request
    ↓
DeutschQuantumSolver.solve()
    ↓
QuantumServerAPI.PostAsync("/deutsch-quantum", funcValues)
    ↓
Quantum Server (ISU_AWS or LOCAL)
    ↓
Returns: {"answer": "constant", "qasm": "OPENQASM..."}
    ↓
Parser extracts "answer"
    ↓
Returns: "constant"
```

For visualizations:

```
Redux Visualization Request
    ↓
DeutschDefaultVisualization.SolvedVisualization()
    ↓
Receives: solution = "constant"
    ↓
Makes own API call: QuantumServerAPI.PostAsync()
    ↓
Extracts: qasm field
    ↓
Returns: {solution: "constant", circuit: "OPENQASM..."}
```

## Notes

- The `solve()` method is synchronous but uses `.Result` on async calls
- Both solver and visualization make calls to the quantum API
- The solver extracts only the answer; the visualization extracts the QASM
- Make sure the quantum servers are running and accessible
- Check firewall/network settings if connection fails
- Default server is ISU_AWS for both solver and visualization

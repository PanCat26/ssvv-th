# Integration Testing Document

## 1. Overview
This document details the Integration Testing strategy used to verify that the `LoanService` interacts correctly with its dependencies, specifically the Data Access Layer (`LibraryDbContext`) and the underlying database provider.

## 2. Integration Strategy
- **Approach**: Bottom-Up / Component Integration.
- **Components Integrated**: `LoanService` (Business Logic Layer) + `LibraryDbContext` (Data Access Layer).
- **Environment**: Entity Framework Core with the `InMemoryDatabase` provider. This ensures tests remain fast and deterministic while fully executing EF Core's mapping, change tracking, and save pipelines.

## 3. Workflow & Data Isolation
To ensure test reliability without needing to seed and clean a physical MySQL database, every test spins up an isolated context:

```csharp
var options = new DbContextOptionsBuilder<LibraryDbContext>()
    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
    .Options;
```
Using a `Guid` guarantees that parallel tests do not interfere with each other's state.

## 4. End-to-End Lifecycle Test

Instead of testing isolated methods, the Integration Test walks through the entire CRUD (Create, Read, Update, Delete) lifecycle of a single entity to ensure the database maintains state across multiple operations.

Implemented in `LoanServiceIntegrationTests.cs`:

### Test Case: `FullLifecycle_CreateUpdateDelete_WorksCorrectly`
1. **Setup**: Seed a new `Book` (2 copies) and a `Member` into the isolated DB.
2. **Action 1 (Create)**: Instantiate `LoanService`, create a loan. 
   - *Assertion*: Database `Book.AvailableCopies` must be decremented to 1.
3. **Action 2 (Update)**: Fetch the loan from the DB, modify the `ReturnDate` to today, and save.
   - *Assertion*: Database `Book.AvailableCopies` must be incremented back to 2.
4. **Action 3 (Delete)**: Call `DeleteAsync` on the loan ID.
   - *Assertion*: The `Loans` table must be empty, and inventory must remain stable at 2.

## 5. Conclusion
The integration tests successfully prove that the `LoanService` can correctly persist state, trigger Entity Framework Core updates, and retrieve accurate relational data, confirming the architectural bridge between the logic and data layers is sound.

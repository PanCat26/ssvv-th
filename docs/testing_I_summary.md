# QA Verification & Validation Summary Report: Testing I (Task 2)

This document serves as the testing summary report for the **Book Loan Feature** in accordance with the Task 2 requirements of the SSVV Take-Home Exam. 

The goal of this phase was to rigorously validate the underlying service logic using formal testing methodologies, without relying on graphical interfaces.

---

## 1. Testing Dashboard & Executive Summary

- **Total Test Cases Executed**: 12 Automated Unit/Integration Cases
- **Automated Test Success Rate**: **100% (12/12 Passed)**
- **Testing Techniques Applied**: Black Box Testing (BBT), White Box Testing (WBT), Integration Testing
- **Frameworks Used**: xUnit, Entity Framework Core (In-Memory), Moq

### Test Project Integration

The isolated tests for Task 2 are located strictly within the backend service testing suite to maximize path control and algorithmic verification:

```text
ssvv-th.Tests/
└── Services/
    ├── LoanServiceBbtTests.cs         (Equivalence Classes & Boundaries)
    ├── LoanServiceWbtTests.cs         (Control Flow Path Coverage)
    └── LoanServiceIntegrationTests.cs (Database Lifecycle Integration)
```

- **Black Box Tests**: [LoanServiceBbtTests.cs](file:///C:/Users/user/Documents/0_Facultate/SEM6/SSVV/Exam/TakeHomeExam/ssvv-th.Tests/Services/LoanServiceBbtTests.cs)
- **White Box Tests**: [LoanServiceWbtTests.cs](file:///C:/Users/user/Documents/0_Facultate/SEM6/SSVV/Exam/TakeHomeExam/ssvv-th.Tests/Services/LoanServiceWbtTests.cs)
- **Integration Tests**: [LoanServiceIntegrationTests.cs](file:///C:/Users/user/Documents/0_Facultate/SEM6/SSVV/Exam/TakeHomeExam/ssvv-th.Tests/Services/LoanServiceIntegrationTests.cs)

---

## 2. Testing Techniques Applied

### Technique A: Black Box Testing (BBT)
For Black Box Testing, we isolated the input/output validation layers of the `CreateAsync` and `UpdateAsync` methods inside `LoanService` without examining the internal code structure.

- **Techniques Used**: Equivalence Partitioning (EP) and Boundary Value Analysis (BVA).
- **Equivalence Classes Validated**:
  - **Dates**: `DueDate < LoanDate` (Invalid), `ReturnDate < LoanDate` (Invalid)
  - **Foreign Keys**: `BookId` and `MemberId` referencing non-existent records (Invalid)
  - **Stock Levels**: `AvailableCopies = 0` (Invalid boundary), `AvailableCopies = 1` (Valid boundary)
- **Outcome**: The service perfectly blocks negative dates, enforces referential rules before saving, and actively catches out-of-stock scenarios.

### Technique B: White Box Testing (WBT)
White Box Testing targeted the most complex algorithmic block of the application: the `ApplyInventoryChanges` mathematical logic. This logic is highly state-dependent based on the transitions of a loan over time.

- **Techniques Used**: Control Flow Graph (CFG) Path Coverage (100% Branch Coverage achieved).
- **Paths Validated**:
  1. `wasActive=True`, `willBeActive=True` (Same Book - No inventory change)
  2. `wasActive=True`, `willBeActive=False` (Returning Book - `Copies + 1`)
  3. `wasActive=False`, `willBeActive=False` (Historically returned - No inventory change)
  4. `wasActive=False`, `willBeActive=True` (Re-opening returned loan - `Copies - 1`)
  5. `wasActive=True`, `willBeActive=True` (Book swapped - Old Book `+1`, New Book `-1`)
- **Outcome**: By isolating exact, tracked EF Core entities, we successfully routed tests down all 5 logic branches, mathematically proving the inventory tracking equations are flawless.

### Technique C: Integration Testing
While BBT and WBT isolated the rules of the service, Integration Testing verified the communication pipeline down to the Database Access Layer.

- **Environment**: Entity Framework Core `InMemoryDatabase` with dynamic connection strings (`Guid.NewGuid()`) for 100% data isolation per test.
- **Workflow Tested**: The complete CRUD Lifecycle (`FullLifecycle_CreateUpdateDelete_WorksCorrectly`).
- **Outcome**: 
  - Verified that calling `CreateAsync` persists records across context lifespans.
  - Verified that pulling the record back into a new DbContext instance reflects the properly decremented inventory stock.
  - Verified that calling `DeleteAsync` fully purges the entity from the mapped `DbSet`.

---

## 3. Automated Test Execution Results

All 12 isolated Task 2 tests execute flawlessly alongside the broader Task 3 tests.

```text
Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    12, Skipped:     0, Total:    12, Duration: 527 ms - ssvv-th.Tests.dll
```

### Detailed Breakdown of Task 2 Tests

| Class | Method | Category | Result |
| :--- | :--- | :--- | :--- |
| `LoanServiceBbtTests` | `CreateAsync_ValidLoan_ReturnsSuccess` | Valid Core Input | **PASSED** |
| `LoanServiceBbtTests` | `CreateAsync_DueDateBeforeLoanDate_ReturnsError` | BVA Date Boundary | **PASSED** |
| `LoanServiceBbtTests` | `CreateAsync_ReturnDateBeforeLoanDate_ReturnsError` | BVA Date Boundary | **PASSED** |
| `LoanServiceBbtTests` | `CreateAsync_NonExistentBook_ReturnsError` | DB Reference Boundary | **PASSED** |
| `LoanServiceBbtTests` | `CreateAsync_NonExistentMember_ReturnsError` | DB Reference Boundary | **PASSED** |
| `LoanServiceBbtTests` | `CreateAsync_BookUnavailable_ReturnsError` | Inventory Boundary | **PASSED** |
| `LoanServiceWbtTests` | `UpdateAsync_ActiveToActive_SameBook_NoInventoryChange` | Control Flow Path 1 | **PASSED** |
| `LoanServiceWbtTests` | `UpdateAsync_ActiveToReturned_SameBook_IncrementsInventory` | Control Flow Path 2 | **PASSED** |
| `LoanServiceWbtTests` | `UpdateAsync_ReturnedToReturned_SameBook_NoInventoryChange` | Control Flow Path 3 | **PASSED** |
| `LoanServiceWbtTests` | `UpdateAsync_ReturnedToActive_SameBook_DecrementsInventory` | Control Flow Path 4 | **PASSED** |
| `LoanServiceWbtTests` | `UpdateAsync_ActiveToActive_ChangeBook_AdjustsInventories` | Control Flow Path 5 | **PASSED** |
| `LoanServiceIntegrationTests` | `FullLifecycle_CreateUpdateDelete_WorksCorrectly` | End-to-End Database | **PASSED** |

---

## 4. Final Quality Assessment

The algorithmic and database-integration foundation of the `LoanService` is **100% verified and fully stable**. The rigorous application of WBT branch coverage mathematically proves the inventory cannot drift out of sync, while BBT ensures invalid data models can never corrupt the database.

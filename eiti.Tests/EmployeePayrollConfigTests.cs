using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using FluentAssertions;

namespace eiti.Tests;

public sealed class EmployeePayrollConfigTests
{
    private static Employee CreateEmployee()
    {
        var companyId = CompanyId.New();
        return Employee.Create(companyId, null, "Juan", "Perez", null, null, null, EmployeeRole.Staff);
    }

    [Fact]
    public void SetPayrollConfig_ShouldSetValues_WhenValid()
    {
        var employee = CreateEmployee();

        employee.SetPayrollConfig(500000m, PayrollPeriodicity.Monthly);

        employee.BaseSalary.Should().Be(500000m);
        employee.PayrollPeriodicity.Should().Be(PayrollPeriodicity.Monthly);
    }

    [Fact]
    public void SetPayrollConfig_ShouldThrow_WhenBaseSalaryNegative()
    {
        var employee = CreateEmployee();

        var act = () => employee.SetPayrollConfig(-1m, PayrollPeriodicity.Monthly);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetPayrollConfig_ShouldThrow_WhenBaseSalarySetWithoutPeriodicity()
    {
        var employee = CreateEmployee();

        var act = () => employee.SetPayrollConfig(500000m, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetPayrollConfig_ShouldClearPeriodicity_WhenBaseSalaryCleared()
    {
        var employee = CreateEmployee();
        employee.SetPayrollConfig(500000m, PayrollPeriodicity.Biweekly);

        employee.SetPayrollConfig(null, null);

        employee.BaseSalary.Should().BeNull();
        employee.PayrollPeriodicity.Should().BeNull();
    }
}

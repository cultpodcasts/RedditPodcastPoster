targetScope = 'subscription'

@description('Enable provisioning of subscription budget alerts.')
param enableAlerts bool = true

@description('Suffix to use for resources')
param suffix string

@description('Resource group filter for budget scope.')
param deploymentResourceGroupName string

@description('Monthly budget amount for Functions meter in billing currency.')
@minValue(1)
param monthlyFunctionsBudgetAmount int

@description('Optional email address for budget notifications.')
param alertEmailAddress string = ''

// Azure Consumption budgets reject updates that change timePeriod.startDate
// ("Start date of budgets cannot be updated"). Do not use utcNow() here —
// a rolling month default fails every redeploy after the budget's create month.
// Keep this aligned with the live budget (functions-cost-budget-infra was created
// 2026-04-01). Changing it requires deleting and recreating the budget.
@description('Immutable budget start date (must match the existing budget once created).')
param budgetStartDate string = '2026-04-01T00:00:00Z'

resource functionsCostBudget 'Microsoft.Consumption/budgets@2023-11-01' = if (enableAlerts) {
  name: 'functions-cost-budget-${suffix}'
  properties: {
    category: 'Cost'
    amount: monthlyFunctionsBudgetAmount
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: budgetStartDate
      endDate: '2036-12-31T00:00:00Z'
    }
    filter: {
      and: [
        {
          dimensions: {
            name: 'ResourceGroupName'
            operator: 'In'
            values: [
              deploymentResourceGroupName
            ]
          }
        }
        {
          dimensions: {
            name: 'ServiceName'
            operator: 'In'
            values: [
              'Functions'
            ]
          }
        }
      ]
    }
    notifications: {
      Actual_GreaterThan_50_Percent: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 50
        contactRoles: [
          'Owner'
          'Contributor'
        ]
        contactEmails: empty(alertEmailAddress) ? [] : [
          alertEmailAddress
        ]
      }
      Actual_GreaterThan_75_Percent: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 75
        contactRoles: [
          'Owner'
          'Contributor'
        ]
        contactEmails: empty(alertEmailAddress) ? [] : [
          alertEmailAddress
        ]
      }
      Actual_GreaterThan_90_Percent: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 90
        contactRoles: [
          'Owner'
          'Contributor'
        ]
        contactEmails: empty(alertEmailAddress) ? [] : [
          alertEmailAddress
        ]
      }
      Actual_GreaterThan_100_Percent: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        contactRoles: [
          'Owner'
          'Contributor'
        ]
        contactEmails: empty(alertEmailAddress) ? [] : [
          alertEmailAddress
        ]
      }
    }
  }
}

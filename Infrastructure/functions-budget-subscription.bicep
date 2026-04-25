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

resource functionsCostBudget 'Microsoft.Consumption/budgets@2023-11-01' = if (enableAlerts) {
  name: 'functions-cost-budget-${suffix}'
  properties: {
    category: 'Cost'
    amount: monthlyFunctionsBudgetAmount
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: '2026-01-01T00:00:00Z'
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

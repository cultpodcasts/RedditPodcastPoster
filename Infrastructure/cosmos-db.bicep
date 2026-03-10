param location string
param cosmosAccountName string

resource cosmosDbAccount 'Microsoft.DocumentDB/databaseAccounts@2025-05-01-preview' = {
  name: cosmosAccountName
  location: location
  kind: 'GlobalDocumentDB'
  tags: {
    defaultExperience: 'Core (SQL)'
  }
  identity: {
    type: 'None'
  }
  properties: {
    databaseAccountOfferType: 'Standard'
    capacityMode: 'Serverless'
    publicNetworkAccess: 'Enabled'
    enableAutomaticFailover: false
    enableMultipleWriteLocations: false
    isVirtualNetworkFilterEnabled: false
    virtualNetworkRules: []
    disableKeyBasedMetadataWriteAccess: false
    enableFreeTier: false
    enableAnalyticalStorage: false
    analyticalStorageConfiguration: {
      schemaType: 'WellDefined'
    }
    createMode: 'Default'
    enableMaterializedViews: false
    enablePartitionMerge: false
    enableBurstCapacity: false
    enablePriorityBasedExecution: false
    networkAclBypass: 'None'
    networkAclBypassResourceIds: []
    disableLocalAuth: false
    minimalTlsVersion: 'Tls12'
    defaultIdentity: 'FirstPartyIdentity'
    enableAllVersionsAndDeletesChangeFeed: false
    enablePerRegionPerPartitionAutoscale: false
    diagnosticLogSettings: {
      enableFullTextQuery: 'None'
    }
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
      maxIntervalInSeconds: 5
      maxStalenessPrefix: 100
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    cors: []
    capabilities: []
    ipRules: []
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-04-15' = {
  parent: cosmosDbAccount
  name: 'cultpodcasts-db'
  properties: {
    resource: {
      id: 'cultpodcasts-db'
    }
  }
}

resource podcastsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: database
  name: 'Podcasts'
  properties: {
    resource: {
      id: 'Podcasts'
      partitionKey: {
        paths: ['/id']
        kind: 'Hash'
      }
    }
    options: {}
  }
}

resource episodesContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: database
  name: 'Episodes'
  properties: {
    resource: {
      id: 'Episodes'
      partitionKey: {
        paths: ['/podcastId']
        kind: 'Hash'
      }
    }
    options: {}
  }
}

resource lookupContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: database
  name: 'LookUps'
  properties: {
    resource: {
      id: 'LookUps'
      partitionKey: {
        paths: ['/id']
        kind: 'Hash'
      }
    }
    options: {}
  }
}

resource pushSubscriptionsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: database
  name: 'PushSubscriptions'
  properties: {
    resource: {
      id: 'PushSubscriptions'
      partitionKey: {
        paths: ['/id']
        kind: 'Hash'
      }
    }
    options: {}
  }
}

resource subjectsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: database
  name: 'Subjects'
  properties: {
    resource: {
      id: 'Subjects'
      partitionKey: {
        paths: ['/id']
        kind: 'Hash'
      }
    }
    options: {}
  }
}

resource discoveryContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: database
  name: 'Discovery'
  properties: {
    resource: {
      id: 'Discovery'
      partitionKey: {
        paths: ['/id']
        kind: 'Hash'
      }
    }
    options: {}
  }
}

resource activityContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: database
  name: 'Activity'
  properties: {
    resource: {
      id: 'Activity'
      partitionKey: {
        paths: ['/id']
        kind: 'Hash'
      }
    }
    options: {}
  }
}

resource bookActivity 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/storedProcedures@2023-04-15' = {
  parent: activityContainer
  name: 'bookActivity'
  properties: {
    resource: {
      id: 'bookActivity'
      body: '''function bookActivity(req) {
    var context= getContext();
    var collection = context.getCollection();
    var collectionLink = collection.getSelfLink();
    const initiate= "initiate", complete= "complete", type= "Activity";
    var isAccepted = collection.queryDocuments(
        collection.getSelfLink(),
        "SELECT * FROM root r where r.type='"+type+"' and r.id='"+req.id+"'",
        function (err, feed, options) {
            if (err) throw err;
            if (!feed || !feed.length) {
                if (req.status != initiate) {
                    throw new Error('No matching Activity Found for status not-initiate: '+JSON.stringify(req));
                }
                var response = context.getResponse();
                var options = { disableAutomaticIdGeneration: false };
                var doc= {
                    id: req.id,
                    status: req.status,
                    type: type,
                    operationType: req.operationType
                };
                var isAccepted = collection.createDocument(
                    collectionLink,
                    doc,
                    options,
                    function (err, newDoc) {
                        if (err) throw new Error('Error: ' + err.message);
                        context.getResponse().setBody(newDoc);
                    }
                );
                response.setBody('no docs found');
            }
            else {
                var response = context.getResponse();
                if (feed.length > 1) {
                    throw new Error('Duplicate Activities: '+JSON.stringify(feed));
                } else {
                    var doc= feed[0];
                    if (doc.status === initiate) {
                        if (req.status == complete) {
                            doc.status= req.status;
                            collection.replaceDocument(doc._self, doc, function (err, updatedDoc) {
                                if (err) throw new Error("Error: " + err.message);
                                context.getResponse().setBody(updatedDoc);
                            });
                        } else if (req.status == initiate) {
                            throw new Error('Activity Already Initiate: '+JSON.stringify(req));
                        } else {
                            throw new Error('Unrecognised incoming status: '+JSON.stringify(req));
                        }
                    } else if (doc.status=== complete) {
                        throw new Error('Activity Already Complete: '+JSON.stringify(doc));
                    } else {
                        throw new Error('Unrecognised status: '+JSON.stringify(doc));
                    }
                }
            }
        }
    );
    if (!isAccepted) throw new Error('The query was not accepted by the server.');
} '''
    }
  }
}

resource concatenateArray 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/userDefinedFunctions@2023-04-15' = {
  parent: activityContainer
  name: 'concatenateArray'
  properties: {
    resource: {
      id: 'concatenateArray'
      body: '''function concatenateArray(arr) {
    return arr.join(", ");
}'''
    }
  }
}

resource joinTerms 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/userDefinedFunctions@2023-04-15' = {
  parent: activityContainer
  name: 'joinTerms'
  properties: {
    resource: {
      id: 'joinTerms'
      body: '''function joinTerms(arr){
    return arr.map((x) => `«${x}»`).join(" ");
}'''
    }
  }
}

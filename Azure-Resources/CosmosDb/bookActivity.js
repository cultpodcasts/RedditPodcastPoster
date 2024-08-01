function bookActivity(req) {
	
	var context= getContext();
    var collection = context.getCollection();
	var collectionLink = collection.getSelfLink();	
	const initiate= 'initiate', complete= 'complete', type= 'Activity';

    var isAccepted = collection.queryDocuments(
        collection.getSelfLink(),
        'SELECT * FROM root r where r.type=\''+type+'\' and r.id=\''+req.id+'\'',
		function (err, feed, options) {
			if (err) throw err;

			if (!feed || !feed.length) {
				if (req.status != initiate) {
					throw new Error('No matching Activity Found for status not-initiate: '+JSON.stringify(req));
				}
				
				var response = context.getResponse();
				
				var options = { disableAutomaticIdGeneration: false };
				var doc= {
					'id': req.id, 
					'status': req.status, 
					'type': type, 
					'operationType': req.operationType
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
}
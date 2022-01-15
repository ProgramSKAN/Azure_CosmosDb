//this Stroed procedure creates new document.(common use case)
//instead of client application saving document direclty to container, it calls Stored Procedure
//SP runs some bussiness logic over the container before saving it
//you can perform some validations and reject the document if validation is failed. 
//or you can alter the document which could involve parsing and cleansing or anything that you can achive with Javascript

//this storedprocedure
//1.attach a boolean property to the address object of new document called 'isNorthAmerica'

//the logic in this SP assumes a certain schema by which it locates the country region and 
//accepts a second parameter 'enforceSchema' that controls how SP behaves if the country 
//region isn't present in the new document

//if 'enforecedSchema' is passed in as true, then SP validates the document and throws an error if expected countryregionName isn't present beneath address property 
//if 'enforecedSchema' is passed in as true, then SP ignore countryRegion

function spSetNorthAmerica(docToCreate, enforceSchema) {
	if (docToCreate.address !== undefined && docToCreate.address.countryRegionName != undefined) {
		docToCreate.address.isNorthAmerica =
			docToCreate.address.countryRegionName === 'United States' ||
			docToCreate.address.countryRegionName === 'Canada' ||
			docToCreate.address.countryRegionName === 'Mexico';
	}
	else if (enforceSchema) {
		throw new Error('Expected document to contain address.countryRegionName property');
		//this terminates SP and rolls back current transaction (ofcourse there nothing to rollback in above code)
	}

	var context = getContext();
	var collection = context.getCollection();//collection(oldName) means container
	var response = context.getResponse();

	//saves document to DB
	collection.createDocument(collection.getSelfLink(), docToCreate, {},
		function (err, docCreated) {
			if (err) {
				throw new Error('Error creating document: ' + err.Message);
			}
			response.setBody(docCreated);
			//docCreated has created document and all system generated properties like resourceId,timeStanp,e-Tag,....
		}
	);
}

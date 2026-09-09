# Azure — Infrastruttura Bicep

Decisioni: Azure, SQL Database a DTU, Blob Storage per immagini, API e frontend separati. Provisioning rimandato come richiesto dall’utente.

Struttura ripresa dal riferimento: modules/ per componenti Bicep riusabili; futuri main.bicep e definizioni di deploy separate be/fe, con parametri per ambiente. Questi file non sono ancora creati.

Da definire: subscription e resource group Fantastiche, regione, hosting dei processi, taglie, domini, accesso Blob e identità applicative. Non utilizzare identificativi o servizi del progetto ACKS.

Bicep crea/configura infrastruttura; migrations EF gestiscono schema SQL; importatore applicativo carica listone e immagini. Sono tre operazioni distinte.

Prima del deploy predisporre parametri senza segreti, identità con accessi necessari e verifica delle modifiche previste. Nessun deploy è eseguito dalla preparazione di queste cartelle.

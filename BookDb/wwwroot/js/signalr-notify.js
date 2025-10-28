'use strict';

var connection = null;

function startSignalR(onReceive) {
    connection = new signalR.HubConnectionBuilder()
        .withUrl('/notify')
        .withAutomaticReconnect()
        .build();

    connection.on('ReceiveNotification', function (message) {
        if (onReceive) onReceive(message);
    });

    connection.start().catch(function (err) {
        console.error(err.toString());
    });
}

function stopSignalR() {
    if (connection) {
        connection.stop();
        connection = null;
    }
}

function sendNotification(message) {
    if (!connection) return;
    connection.invoke('SendNotification', message).catch(function (err) {
        console.error(err.toString());
    });
}

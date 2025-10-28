// Bookmark SignalR Integration
(function () {
    'use strict';

    window.BookmarkSignalR = {
        connection: null,
        isInitialized: false,

        init: function (onBookmarkCreated, onBookmarkDeleted, onBookmarkUpdated) {
            if (this.isInitialized) {
                console.log('Bookmark SignalR already initialized');
                return;
            }

            // Use the global NotificationHub connection
            if (!window.NotificationHub || !window.NotificationHub.connection) {
                console.warn('NotificationHub not ready, retrying...');
                setTimeout(() => this.init(onBookmarkCreated, onBookmarkDeleted, onBookmarkUpdated), 500);
                return;
            }

            this.connection = window.NotificationHub.connection;
            this.setupBookmarkHandlers(onBookmarkCreated, onBookmarkDeleted, onBookmarkUpdated);
            this.isInitialized = true;
        },

        setupBookmarkHandlers: function (onCreated, onDeleted, onUpdated) {
            // Handle bookmark created
            this.connection.on('BookmarkCreated', (data) => {
                console.log('BookmarkCreated event received:', data);
                if (onCreated && typeof onCreated === 'function') {
                    onCreated(data);
                }
            });

            // Handle bookmark deleted
            this.connection.on('BookmarkDeleted', (data) => {
                console.log('BookmarkDeleted event received:', data);
                if (onDeleted && typeof onDeleted === 'function') {
                    onDeleted(data);
                }
            });

            // Handle bookmark updated
            this.connection.on('BookmarkUpdated', (data) => {
                console.log('BookmarkUpdated event received:', data);
                if (onUpdated && typeof onUpdated === 'function') {
                    onUpdated(data);
                }
            });
        },

        // Helper to notify when bookmark is created
        notifyBookmarkCreated: function (bookmarkId, title, documentId, pageNumber) {
            if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
                console.warn('SignalR not connected');
                return Promise.reject('Not connected');
            }

            return this.connection.invoke('NotifyBookmarkCreated', bookmarkId, title, documentId, pageNumber)
                .catch(err => console.error('Error notifying bookmark created:', err));
        },

        // Helper to notify when bookmark is deleted
        notifyBookmarkDeleted: function (bookmarkId, title) {
            if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
                console.warn('SignalR not connected');
                return Promise.reject('Not connected');
            }

            return this.connection.invoke('NotifyBookmarkDeleted', bookmarkId, title)
                .catch(err => console.error('Error notifying bookmark deleted:', err));
        },

        // Helper to notify when bookmark is updated
        notifyBookmarkUpdated: function (bookmarkId, title) {
            if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
                console.warn('SignalR not connected');
                return Promise.reject('Not connected');
            }

            return this.connection.invoke('NotifyBookmarkUpdated', bookmarkId, title)
                .catch(err => console.error('Error notifying bookmark updated:', err));
        },

        cleanup: function () {
            if (this.connection) {
                this.connection.off('BookmarkCreated');
                this.connection.off('BookmarkDeleted');
                this.connection.off('BookmarkUpdated');
            }
            this.isInitialized = false;
        }
    };

    // Cleanup on page unload
    window.addEventListener('beforeunload', function () {
        if (window.BookmarkSignalR) {
            window.BookmarkSignalR.cleanup();
        }
    });

})();

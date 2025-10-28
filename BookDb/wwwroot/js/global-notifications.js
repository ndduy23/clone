// Global SignalR Notification System
(function () {
    'use strict';

    let connection = null;
    let notificationCount = 0;
    let notifications = [];
    const MAX_NOTIFICATIONS = 50;

    // Initialize NotificationHub global object
    window.NotificationHub = {
        connection: null,
        isConnected: false,
        notifications: [],

        init: function () {
            this.createConnection();
            this.setupEventHandlers();
            this.startConnection();
        },

        createConnection: function () {
            if (typeof signalR === 'undefined') {
                console.warn('SignalR not loaded yet, retrying...');
                setTimeout(() => this.init(), 100);
                return;
            }

            connection = new signalR.HubConnectionBuilder()
                .withUrl('/notify')
                .withAutomaticReconnect()
                .configureLogging(signalR.LogLevel.Information)
                .build();

            this.connection = connection;
        },

        setupEventHandlers: function () {
            // Track if we already added this notification to prevent duplicates
            const processedNotifications = new Set();
            
            // Handle general notifications
            connection.on('ReceiveNotification', (message) => {
                console.log('Notification received:', message);
                
                // Create unique key for this notification
                const notificationKey = `${message}_${Date.now()}`;
                
                // Only process if not duplicate within 1 second
                if (!processedNotifications.has(message)) {
                    processedNotifications.add(message);
                    this.addNotification(message, 'info');
                    this.showToast(message, 'info');
                    
                    // Remove from set after 1 second to allow same message later
                    setTimeout(() => processedNotifications.delete(message), 1000);
                }
            });

            // Handle page changes (only show toast, not duplicate in panel)
            connection.on('PageAdded', (data) => {
                const message = `📄 Trang mới được thêm: Trang ${data.PageNumber}`;
                this.showToast(message, 'success');
            });

            connection.on('PageUpdated', (data) => {
                const message = `✏️ Trang ${data.PageNumber} đã được cập nhật`;
                this.showToast(message, 'info');
            });

            connection.on('PageDeleted', (data) => {
                const message = `🗑️ Trang ${data.PageNumber} đã bị xóa`;
                this.showToast(message, 'warning');
            });

            // Handle document changes (only show toast for important events)
            connection.on('DocumentAdded', (data) => {
                const message = `📚 Tài liệu mới: ${data.Title || 'Không có tiêu đề'}`;
                this.showToast(message, 'success');
            });

            connection.on('DocumentUpdated', (data) => {
                const message = `✏️ Tài liệu đã được cập nhật: ${data.Title || ''}`;
                this.showToast(message, 'info');
            });

            connection.on('DocumentDeleted', (data) => {
                const message = `🗑️ Tài liệu đã bị xóa: ${data.Title || ''}`;
                this.showToast(message, 'warning');
            });

            // Handle bookmark changes
            connection.on('BookmarkCreated', (data) => {
                const message = `🔖 Bookmark mới: ${data.DocumentTitle || ''} - Trang ${data.PageNumber || ''}`;
                this.showToast(message, 'success');
            });

            connection.on('BookmarkDeleted', (data) => {
                const message = `🗑️ Bookmark đã bị xóa: ${data.Title || ''}`;
                this.showToast(message, 'warning');
            });

            // Connection state handlers
            connection.onreconnecting((error) => {
                console.log('Reconnecting...', error);
                this.isConnected = false;
                this.showToast('Đang kết nối lại...', 'warning');
            });

            connection.onreconnected((connectionId) => {
                console.log('Reconnected:', connectionId);
                this.isConnected = true;
                this.showToast('Đã kết nối lại thành công', 'success');
            });

            connection.onclose((error) => {
                console.log('Connection closed', error);
                this.isConnected = false;
                setTimeout(() => this.startConnection(), 5000);
            });
        },

        startConnection: function () {
            connection.start()
                .then(() => {
                    console.log('SignalR Connected');
                    this.isConnected = true;
                    this.updateConnectionStatus(true);
                })
                .catch((err) => {
                    console.error('SignalR Connection Error:', err);
                    this.isConnected = false;
                    this.updateConnectionStatus(false);
                    setTimeout(() => this.startConnection(), 5000);
                });
        },

        addNotification: function (message, type = 'info') {
            const notification = {
                id: Date.now(),
                message: message,
                type: type,
                timestamp: new Date(),
                read: false
            };

            notifications.unshift(notification);
            this.notifications = notifications;

            // Keep only the last MAX_NOTIFICATIONS
            if (notifications.length > MAX_NOTIFICATIONS) {
                notifications = notifications.slice(0, MAX_NOTIFICATIONS);
                this.notifications = notifications;
            }

            notificationCount++;
            this.updateNotificationCount();
            this.updateNotificationPanel();
        },

        showToast: function (message, type = 'info') {
            const toast = document.createElement('div');
            const typeClasses = {
                'info': 'alert-info',
                'success': 'alert-success',
                'warning': 'alert-warning',
                'error': 'alert-danger',
                'danger': 'alert-danger'
            };

            const icons = {
                'info': 'ℹ️',
                'success': '✅',
                'warning': '⚠️',
                'error': '❌',
                'danger': '❌'
            };

            toast.className = `alert ${typeClasses[type] || 'alert-info'} notification-toast`;
            toast.style.cssText = `
                position: fixed;
                top: 80px;
                right: 20px;
                z-index: 10000;
                min-width: 300px;
                max-width: 400px;
                padding: 15px;
                border-radius: 8px;
                box-shadow: 0 4px 12px rgba(0,0,0,0.15);
                animation: slideInRight 0.3s ease-out;
            `;

            toast.innerHTML = `
                <div style="display: flex; align-items: start; justify-content: space-between;">
                    <div>
                        <strong>${icons[type] || 'ℹ️'} Thông báo</strong><br>
                        <span>${this.escapeHtml(message)}</span>
                    </div>
                    <button type="button" class="btn-close ms-3" onclick="this.closest('.notification-toast').remove()"></button>
                </div>
            `;

            document.body.appendChild(toast);

            // Auto remove after 5 seconds
            setTimeout(() => {
                if (toast.parentElement) {
                    toast.style.animation = 'slideOutRight 0.3s ease-in';
                    setTimeout(() => toast.remove(), 300);
                }
            }, 5000);
        },

        showLocal: function (message, type = 'info') {
            this.showToast(message, type);
        },

        updateNotificationCount: function () {
            const badge = document.getElementById('notificationCount');
            if (badge) {
                const unreadCount = notifications.filter(n => !n.read).length;
                if (unreadCount > 0) {
                    badge.textContent = unreadCount > 99 ? '99+' : unreadCount;
                    badge.style.display = 'inline';
                } else {
                    badge.style.display = 'none';
                }
            }
        },

        updateNotificationPanel: function () {
            const panel = document.getElementById('notificationPanel');
            if (!panel) return;

            const list = panel.querySelector('.notification-list');
            if (!list) return;

            if (notifications.length === 0) {
                list.innerHTML = '<div class="text-center text-muted p-4">Không có thông báo</div>';
                return;
            }

            list.innerHTML = notifications.map(n => `
                <div class="notification-item ${n.read ? 'read' : 'unread'}" data-id="${n.id}">
                    <div class="d-flex justify-content-between align-items-start">
                        <div class="flex-grow-1">
                            <div class="notification-message">${this.escapeHtml(n.message)}</div>
                            <small class="text-muted">${this.formatTime(n.timestamp)}</small>
                        </div>
                        <button class="btn btn-sm btn-link text-danger" onclick="window.NotificationHub.removeNotification(${n.id})">
                            ×
                        </button>
                    </div>
                </div>
            `).join('');
        },

        toggleNotificationPanel: function () {
            const panel = document.getElementById('notificationPanel');
            if (!panel) return;

            const isVisible = panel.style.display === 'block';
            panel.style.display = isVisible ? 'none' : 'block';

            if (!isVisible) {
                // Mark all as read
                notifications.forEach(n => n.read = true);
                this.updateNotificationCount();
                this.updateNotificationPanel();
            }
        },

        removeNotification: function (id) {
            notifications = notifications.filter(n => n.id !== id);
            this.notifications = notifications;
            this.updateNotificationPanel();
            this.updateNotificationCount();
        },

        clearAllNotifications: function () {
            if (confirm('Xóa tất cả thông báo?')) {
                notifications = [];
                this.notifications = [];
                this.updateNotificationPanel();
                this.updateNotificationCount();
            }
        },

        updateConnectionStatus: function (isConnected) {
            const statusIndicator = document.getElementById('connectionStatus');
            if (statusIndicator) {
                statusIndicator.className = isConnected ? 'text-success' : 'text-danger';
                statusIndicator.textContent = isConnected ? '● Đã kết nối' : '● Mất kết nối';
            }
        },

        sendNotification: function (message) {
            if (!this.isConnected || !connection) {
                console.warn('Not connected to SignalR');
                return Promise.reject('Not connected');
            }

            return connection.invoke('SendNotification', message)
                .catch(err => console.error('Error sending notification:', err));
        },

        escapeHtml: function (text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        },

        formatTime: function (date) {
            const now = new Date();
            const diff = Math.floor((now - date) / 1000); // seconds

            if (diff < 60) return 'Vừa xong';
            if (diff < 3600) return `${Math.floor(diff / 60)} phút trước`;
            if (diff < 86400) return `${Math.floor(diff / 3600)} giờ trước`;
            
            return date.toLocaleDateString('vi-VN', { 
                day: '2-digit', 
                month: '2-digit', 
                year: 'numeric',
                hour: '2-digit',
                minute: '2-digit'
            });
        }
    };

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            window.NotificationHub.init();
        });
    } else {
        window.NotificationHub.init();
    }

    // Close notification panel when clicking outside
    document.addEventListener('click', function (e) {
        const panel = document.getElementById('notificationPanel');
        const icon = document.getElementById('notificationIcon');
        
        if (panel && icon && 
            panel.style.display === 'block' && 
            !panel.contains(e.target) && 
            !icon.contains(e.target)) {
            panel.style.display = 'none';
        }
    });

})();

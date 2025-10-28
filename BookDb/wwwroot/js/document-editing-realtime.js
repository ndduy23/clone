// Real-time Document Editing Notifications
(function () {
    'use strict';

    window.DocumentEditingRealtime = {
        connection: null,
        currentDocumentId: null,
        isEditing: false,
        userName: 'User', // Default, should be set from server
        editingUsers: new Set(),

        init: function (documentId, userName = 'User') {
            this.currentDocumentId = documentId;
            this.userName = userName;

            // Use global NotificationHub connection
            if (!window.NotificationHub || !window.NotificationHub.connection) {
                console.warn('NotificationHub not ready');
                setTimeout(() => this.init(documentId, userName), 500);
                return;
            }

            this.connection = window.NotificationHub.connection;
            this.setupHandlers();
        },

        setupHandlers: function () {
            // Listen for someone starting to edit
            this.connection.on('DocumentEditingStarted', (data) => {
                if (data.DocumentId === this.currentDocumentId) {
                    this.editingUsers.add(data.UserName);
                    this.showEditingIndicator(data.UserName, data.DocumentTitle);
                    console.log(`${data.UserName} started editing document ${data.DocumentId}`);
                }
            });

            // Listen for someone stopping editing
            this.connection.on('DocumentEditingEnded', (data) => {
                if (data.DocumentId === this.currentDocumentId) {
                    this.editingUsers.delete(data.UserName);
                    this.hideEditingIndicator(data.UserName);
                    console.log(`${data.UserName} stopped editing document ${data.DocumentId}`);
                }
            });

            // Listen for field changes in real-time
            this.connection.on('DocumentFieldChanged', (data) => {
                if (data.DocumentId === this.currentDocumentId) {
                    this.updateField(data.FieldName, data.NewValue, data.UserName);
                    console.log(`${data.UserName} changed ${data.FieldName} to: ${data.NewValue}`);
                }
            });
        },

        startEditing: function (documentTitle) {
            if (this.isEditing) return;

            this.isEditing = true;
            
            if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
                this.connection.invoke('NotifyDocumentEditingStarted', 
                    this.currentDocumentId, 
                    documentTitle, 
                    this.userName
                ).catch(err => console.error('Error notifying edit start:', err));
            }
        },

        stopEditing: function () {
            if (!this.isEditing) return;

            this.isEditing = false;

            if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
                this.connection.invoke('NotifyDocumentEditingEnded', 
                    this.currentDocumentId, 
                    this.userName
                ).catch(err => console.error('Error notifying edit end:', err));
            }
        },

        notifyFieldChange: function (fieldName, newValue) {
            if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
                this.connection.invoke('NotifyDocumentFieldChanged', 
                    this.currentDocumentId, 
                    fieldName, 
                    newValue, 
                    this.userName
                ).catch(err => console.error('Error notifying field change:', err));
            }
        },

        showEditingIndicator: function (userName, documentTitle) {
            // Remove existing indicator for this user if any
            $(`#editing-indicator-${this.sanitizeUserName(userName)}`).remove();

            // Create new indicator
            const indicator = $(`
                <div id="editing-indicator-${this.sanitizeUserName(userName)}" 
                     class="alert alert-info alert-dismissible fade show editing-indicator" 
                     role="alert">
                    <strong>👤 ${this.escapeHtml(userName)}</strong> đang chỉnh sửa tài liệu này
                    <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
                </div>
            `);

            // Add to page
            if ($('#editing-indicators-container').length === 0) {
                $('main').prepend('<div id="editing-indicators-container"></div>');
            }
            $('#editing-indicators-container').append(indicator);

            // Show toast notification
            window.NotificationHub.showLocal(
                `👤 ${userName} đang chỉnh sửa tài liệu`, 
                'info'
            );
        },

        hideEditingIndicator: function (userName) {
            $(`#editing-indicator-${this.sanitizeUserName(userName)}`).fadeOut(300, function() {
                $(this).remove();
            });

            window.NotificationHub.showLocal(
                `${userName} đã kết thúc chỉnh sửa`, 
                'info'
            );
        },

        updateField: function (fieldName, newValue, userName) {
            // Find the field and update it with visual feedback
            const $field = $(`[name="${fieldName}"], #${fieldName}`);
            
            if ($field.length > 0) {
                // Show temporary highlight
                $field.addClass('field-updated-by-other');
                
                // Update value if it's an input/textarea/select
                if ($field.is('input, textarea, select')) {
                    const currentValue = $field.val();
                    if (currentValue !== newValue) {
                        $field.val(newValue);
                        
                        // Show notification
                        window.NotificationHub.showLocal(
                            `${userName} đã thay đổi "${this.getFieldLabel(fieldName)}"`, 
                            'info'
                        );
                    }
                } else {
                    // For display elements, update text
                    $field.text(newValue);
                }

                // Remove highlight after 2 seconds
                setTimeout(() => {
                    $field.removeClass('field-updated-by-other');
                }, 2000);
            }
        },

        getFieldLabel: function (fieldName) {
            const labels = {
                'title': 'Tiêu đề',
                'category': 'Lĩnh vực',
                'author': 'Tác giả',
                'description': 'Mô tả'
            };
            return labels[fieldName] || fieldName;
        },

        sanitizeUserName: function (userName) {
            return userName.replace(/[^a-zA-Z0-9]/g, '_');
        },

        escapeHtml: function (text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        },

        cleanup: function () {
            this.stopEditing();
            
            if (this.connection) {
                this.connection.off('DocumentEditingStarted');
                this.connection.off('DocumentEditingEnded');
                this.connection.off('DocumentFieldChanged');
            }
        }
    };

    // Auto cleanup on page unload
    window.addEventListener('beforeunload', function () {
        if (window.DocumentEditingRealtime) {
            window.DocumentEditingRealtime.cleanup();
        }
    });

})();

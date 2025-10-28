// wwwroot/js/document-list-notify.js
// SignalR client for document list pages (Index, Search results, etc.)

let listConnection = null;

function startDocumentListNotify() {
    if (typeof signalR === 'undefined') {
        console.warn('SignalR not loaded yet, retrying...');
        setTimeout(startDocumentListNotify, 100);
        return;
    }

    // Create SignalR connection
    listConnection = new signalR.HubConnectionBuilder()
        .withUrl("/notificationHub")
        .withAutomaticReconnect()
        .build();

    // Handle general notifications (new document uploaded, etc.)
    listConnection.on("ReceiveNotification", function (message) {
        console.log("Notification received:", message);
        showListNotification(message, 'info');

        // Show reload prompt for document additions/deletions
        if (message.includes("uploaded") || message.includes("deleted")) {
            setTimeout(function () {
                if (confirm('Có thay ??i trong danh sách tài li?u. T?i l?i trang?')) {
                    location.reload();
                }
            }, 1500);
        }
    });

    // Handle document updates (metadata changes)
    listConnection.on("DocumentUpdated", function (data) {
        console.log("Document updated:", data);
        showListNotification('Tài li?u ?ã ???c c?p nh?t', 'info');
        highlightDocumentRow(data.DocumentId);
    });

    // Handle document deletion
    listConnection.on("DocumentDeleted", function (data) {
        console.log("Document deleted:", data);
        showListNotification('Tài li?u ?ã b? xóa', 'warning');
        removeDocumentRow(data.DocumentId);
    });

    // Handle new document added
    listConnection.on("DocumentAdded", function (data) {
        console.log("Document added:", data);
        showListNotification('Tài li?u m?i: ' + (data.Title || 'Không có tiêu ??'), 'success');

        setTimeout(function () {
            if (confirm('Có tài li?u m?i ???c thêm. T?i l?i ?? xem?')) {
                location.reload();
            }
        }, 1500);
    });

    // Handle reconnection
    listConnection.onreconnected(connectionId => {
        console.log("List view reconnected:", connectionId);
        showListNotification('K?t n?i l?i thành công', 'success');
    });

    listConnection.onreconnecting(error => {
        console.log("List view reconnecting...", error);
        showListNotification('?ang k?t n?i l?i...', 'warning');
    });

    listConnection.onclose(error => {
        console.log("List view connection closed", error);
    });

    // Start connection
    listConnection.start()
        .then(function () {
            console.log("SignalR connected for document list");
        })
        .catch(function (err) {
            console.error("SignalR connection error:", err);
        });
}

function stopDocumentListNotify() {
    if (listConnection) {
        listConnection.stop()
            .then(() => console.log("List view connection stopped"))
            .catch(err => console.error("Error stopping connection:", err));
    }
}

function showListNotification(message, type) {
    // Create notification element
    const notification = document.createElement('div');

    const typeClasses = {
        'info': 'alert-info',
        'success': 'alert-success',
        'warning': 'alert-warning',
        'error': 'alert-danger'
    };

    notification.className = `alert ${typeClasses[type] || 'alert-info'} notification-toast`;
    notification.style.cssText = `
        position: fixed;
        top: 20px;
        right: 20px;
        z-index: 9999;
        min-width: 300px;
        max-width: 500px;
        padding: 15px;
        border-radius: 5px;
        box-shadow: 0 4px 6px rgba(0,0,0,0.1);
        animation: slideIn 0.3s ease-out;
    `;

    const icons = {
        'info': '??',
        'success': '?',
        'warning': '??',
        'error': '?'
    };

    notification.innerHTML = `
        <div style="display: flex; align-items: center; justify-content: space-between;">
            <div>
                <strong>${icons[type] || '??'} ${type === 'error' ? 'L?i' : type === 'warning' ? 'C?nh báo' : type === 'success' ? 'Thành công' : 'Thông báo'}</strong><br>
                <span>${message}</span>
            </div>
            <button type="button" class="btn-close ms-3" onclick="this.closest('.notification-toast').remove()"></button>
        </div>
    `;

    document.body.appendChild(notification);

    // Auto remove after 5 seconds
    setTimeout(function () {
        if (notification.parentElement) {
            notification.style.animation = 'slideOut 0.3s ease-in';
            setTimeout(() => notification.remove(), 300);
        }
    }, 5000);
}

function highlightDocumentRow(documentId) {
    const row = document.querySelector(`tr[data-document-id="${documentId}"]`);
    if (row) {
        // Flash animation
        row.style.backgroundColor = '#fff3cd';
        row.style.transition = 'background-color 0.5s ease';

        // Pulse effect
        let pulses = 0;
        const pulseInterval = setInterval(function () {
            pulses++;
            row.style.backgroundColor = pulses % 2 === 0 ? '#fff3cd' : '';

            if (pulses >= 4) {
                clearInterval(pulseInterval);
                row.style.backgroundColor = '';
            }
        }, 300);
    }
}

function removeDocumentRow(documentId) {
    const row = document.querySelector(`tr[data-document-id="${documentId}"]`);
    if (row) {
        // Fade out and slide animation
        row.style.backgroundColor = '#f8d7da';
        row.style.transition = 'all 0.5s ease';

        setTimeout(function () {
            row.style.opacity = '0';
            row.style.transform = 'translateX(-100%)';

            setTimeout(function () {
                row.remove();

                // Check if table is empty
                const tbody = row.closest('tbody');
                if (tbody && tbody.children.length === 0) {
                    const colCount = row.querySelectorAll('td').length;
                    tbody.innerHTML = `<tr><td colspan="${colCount}" class="text-center text-muted py-4">Không có tài li?u nào</td></tr>`;
                }
            }, 500);
        }, 500);
    }
}

function addDocumentRow(document) {
    const tbody = document.querySelector('#documentsTable tbody');
    if (!tbody) return;

    // Remove "no documents" message if exists
    const emptyRow = tbody.querySelector('td[colspan]');
    if (emptyRow) {
        emptyRow.closest('tr').remove();
    }

    // Create new row
    const newRow = document.createElement('tr');
    newRow.setAttribute('data-document-id', document.Id);
    newRow.style.backgroundColor = '#d1e7dd';
    newRow.innerHTML = `
        <td>${escapeHtml(document.Title || '')}</td>
        <td>${escapeHtml(document.Category || '')}</td>
        <td>${escapeHtml(document.Author || '')}</td>
        <td>${formatDate(document.CreatedAt)}</td>
        <td>
            <a href="/documents/view/${document.Id}" class="btn btn-sm btn-success">Xem</a>
            <a href="/documents/edit/${document.Id}" class="btn btn-sm btn-warning">S?a</a>
            <form action="/documents/delete/${document.Id}" method="post" style="display:inline">
                <button type="submit" class="btn btn-sm btn-danger" onclick="return confirm('Xóa tài li?u này?')">Xóa</button>
            </form>
        </td>
    `;

    // Insert at the beginning
    tbody.insertBefore(newRow, tbody.firstChild);

    // Fade in animation
    setTimeout(function () {
        newRow.style.transition = 'background-color 2s ease';
        newRow.style.backgroundColor = '';
    }, 100);
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleDateString('vi-VN');
}

// Add CSS animations if not already present
if (!document.getElementById('notification-styles')) {
    const style = document.createElement('style');
    style.id = 'notification-styles';
    style.textContent = `
        @keyframes slideIn {
            from {
                transform: translateX(400px);
                opacity: 0;
            }
            to {
                transform: translateX(0);
                opacity: 1;
            }
        }
        @keyframes slideOut {
            from {
                transform: translateX(0);
                opacity: 1;
            }
            to {
                transform: translateX(400px);
                opacity: 0;
            }
        }
    `;
    document.head.appendChild(style);
}
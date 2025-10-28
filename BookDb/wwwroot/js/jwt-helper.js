// JWT Helper Functions
window.JwtHelper = (function () {
    'use strict';

    // Get token from localStorage
    function getToken() {
        return localStorage.getItem('token');
    }

    // Get refresh token from localStorage
    function getRefreshToken() {
        return localStorage.getItem('refreshToken');
    }

    // Save tokens to localStorage and cookies
    function saveTokens(token, refreshToken) {
        localStorage.setItem('token', token);
        localStorage.setItem('refreshToken', refreshToken);

        // Also save to cookies for server-side access
        document.cookie = `token=${token}; path=/; max-age=3600; SameSite=Lax`;
        document.cookie = `refreshToken=${refreshToken}; path=/; max-age=604800; SameSite=Lax`;
    }

    // Clear tokens from localStorage and cookies
    function clearTokens() {
        localStorage.removeItem('token');
        localStorage.removeItem('refreshToken');
        localStorage.removeItem('user');

        // Clear cookies
        document.cookie = 'token=; path=/; max-age=0';
        document.cookie = 'refreshToken=; path=/; max-age=0';
    }

    // Get user from localStorage
    function getUser() {
        const userJson = localStorage.getItem('user');
        return userJson ? JSON.parse(userJson) : null;
    }

    // Save user to localStorage
    function saveUser(user) {
        localStorage.setItem('user', JSON.stringify(user));
    }

    // Check if user is authenticated
    function isAuthenticated() {
        const token = getToken();
        if (!token) return false;

        // Check if token is expired
        try {
            const payload = parseJwt(token);
            const exp = payload.exp * 1000; // Convert to milliseconds
            return Date.now() < exp;
        } catch (e) {
            return false;
        }
    }

    // Parse JWT token
    function parseJwt(token) {
        try {
            const base64Url = token.split('.')[1];
            const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
            const jsonPayload = decodeURIComponent(atob(base64).split('').map(function (c) {
                return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
            }).join(''));
            return JSON.parse(jsonPayload);
        } catch (e) {
            console.error('Error parsing JWT:', e);
            return null;
        }
    }

    // Add authorization header to AJAX request
    function addAuthHeader(xhr) {
        const token = getToken();
        if (token) {
            xhr.setRequestHeader('Authorization', 'Bearer ' + token);
        }
    }

    // Refresh token
    async function refreshToken() {
        const token = getToken();
        const refreshToken = getRefreshToken();

        if (!token || !refreshToken) {
            console.error('No tokens found');
            return false;
        }

        try {
            const response = await fetch('/api/auth/refresh-token', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    token: token,
                    refreshToken: refreshToken
                })
            });

            const data = await response.json();

            if (data.success) {
                saveTokens(data.token, data.refreshToken);
                saveUser(data.user);
                return true;
            } else {
                clearTokens();
                return false;
            }
        } catch (error) {
            console.error('Error refreshing token:', error);
            clearTokens();
            return false;
        }
    }

    // Logout
    function logout() {
        clearTokens();
        window.location.href = '/auth/login';
    }

    // Setup AJAX defaults to include JWT token
    function setupAjaxDefaults() {
        $.ajaxSetup({
            beforeSend: function (xhr) {
                const token = getToken();
                if (token) {
                    xhr.setRequestHeader('Authorization', 'Bearer ' + token);
                }
            },
            error: function (xhr) {
                if (xhr.status === 401) {
                    // Unauthorized - try to refresh token
                    refreshToken().then(success => {
                        if (!success) {
                            // Refresh failed - redirect to login
                            logout();
                        }
                    });
                }
            }
        });
    }

    // Check authentication and redirect if needed
    function requireAuth() {
        if (!isAuthenticated()) {
            window.location.href = '/auth/login';
            return false;
        }
        return true;
    }

    // Update UI based on authentication status
    function updateAuthUI() {
        const user = getUser();
        const isAuth = isAuthenticated();

        if (isAuth && user) {
            // User is logged in
            $('#authUserName').text(user.fullName || user.email);
            $('#authUserEmail').text(user.email);
            $('.auth-required').show();
            $('.guest-only').hide();
        } else {
            // User is not logged in
            $('.auth-required').hide();
            $('.guest-only').show();
        }
    }

    // Initialize on page load
    $(document).ready(function () {
        setupAjaxDefaults();
        updateAuthUI();

        // Handle logout button
        $(document).on('click', '.logout-btn', function (e) {
            e.preventDefault();
            logout();
        });
    });

    // Public API
    return {
        getToken: getToken,
        getRefreshToken: getRefreshToken,
        saveTokens: saveTokens,
        clearTokens: clearTokens,
        getUser: getUser,
        saveUser: saveUser,
        isAuthenticated: isAuthenticated,
        parseJwt: parseJwt,
        addAuthHeader: addAuthHeader,
        refreshToken: refreshToken,
        logout: logout,
        setupAjaxDefaults: setupAjaxDefaults,
        requireAuth: requireAuth,
        updateAuthUI: updateAuthUI
    };
})();
async function testConnection() {
    try {
        const response = await fetch('/api/graph/test-connection');
        const data = await response.json();
        
        const content = document.getElementById('content');
        
        if (data.isConnected) {
            content.innerHTML = renderSuccessView(data);
        } else {
            content.innerHTML = renderErrorView(data);
        }
    } catch (error) {
        document.getElementById('content').innerHTML = renderCriticalError(error);
    }
}

function renderSuccessView(data) {
    const usersHtml = data.users.map((user, index) => `
        <div class='info-box user-card'>
            <div class='user-name'>
                ${index + 1}. ${user.displayName}
            </div>
            <div class='info-row'>
                <div class='info-label'>Email:</div>
                <div class='info-value'>${user.email}</div>
            </div>
            <div class='info-row'>
                <div class='info-label'>ID:</div>
                <div class='info-value' style='font-size: 12px;'>${user.userId}</div>
            </div>
            <div class='info-row'>
                <div class='info-label'>Stanowisko:</div>
                <div class='info-value'>${user.jobTitle}</div>
            </div>
        </div>
    `).join('');

    return `
        <div class='status-box status-success'>
            <div class='status-icon'>✅</div>
            <div class='status-text'>
                <div class='status-title'>Połączenie udane!</div>
                <div class='status-message'>Microsoft Graph API działa poprawnie</div>
            </div>
        </div>
        
        <div class='info-box'>
            <div class='info-row'>
                <div class='info-label'>Liczba użytkowników:</div>
                <div class='info-value'><strong>${data.totalUsers}</strong></div>
            </div>
            <div class='info-row'>
                <div class='info-label'>Czas odpowiedzi:</div>
                <div class='info-value'>${data.responseTime}</div>
            </div>
        </div>
        
        <h2>👥 Lista użytkowników</h2>
        <div class='users-list'>
            ${usersHtml}
        </div>
        
        <button class='btn' onclick='testConnection()'>🔄 Odśwież test</button>
    `;
}

function renderErrorView(data) {
    return `
        <div class='status-box status-error'>
            <div class='status-icon'>❌</div>
            <div class='status-text'>
                <div class='status-title'>Błąd połączenia</div>
                <div class='status-message'>Nie udało się połączyć z Microsoft Graph API</div>
            </div>
        </div>
        
        <div class='info-box'>
            <div class='info-row'>
                <div class='info-label'>Komunikat błędu:</div>
                <div class='info-value' style='color: #dc3545;'>${data.error}</div>
            </div>
            <div class='info-row'>
                <div class='info-label'>Czas próby:</div>
                <div class='info-value'>${data.responseTime}</div>
            </div>
        </div>
        
        <button class='btn' onclick='testConnection()'>🔄 Spróbuj ponownie</button>
    `;
}

function renderCriticalError(error) {
    return `
        <div class='status-box status-error'>
            <div class='status-icon'>❌</div>
            <div class='status-text'>
                <div class='status-title'>Błąd aplikacji</div>
                <div class='status-message'>Wystąpił problem z aplikacją</div>
            </div>
        </div>
        <div class='info-box'>
            <div class='info-row'>
                <div class='info-label'>Szczegóły:</div>
                <div class='info-value' style='color: #dc3545;'>${error.message}</div>
            </div>
        </div>
        <button class='btn' onclick='testConnection()'>🔄 Spróbuj ponownie</button>
    `;
}

document.addEventListener('DOMContentLoaded', testConnection);
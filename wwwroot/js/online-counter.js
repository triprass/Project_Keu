document.addEventListener("DOMContentLoaded", function () {
    let visitorId = localStorage.getItem('visitor_uuid');
    if (!visitorId) {
        visitorId = 'user_' + Math.random().toString(36).substring(2) + Date.now().toString(36);
        localStorage.setItem('visitor_uuid', visitorId);
    }

    async function updateOnlineStatus() {
        try {
            // Memanggil API global
            const response = await fetch(`/api/heartbeat?id=${visitorId}`);
            if (response.ok) {
                const data = await response.json();
                const countElement = document.getElementById('online-count');
                if (countElement && data.count !== undefined) {
                    countElement.innerText = data.count;
                }
            }
        } catch (err) {
            console.error('Gagal mengambil data user online: ', err);
        }
    }

    updateOnlineStatus();
    setInterval(updateOnlineStatus, 5000);
});
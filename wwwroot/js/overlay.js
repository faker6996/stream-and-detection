// wwwroot/js/overlay.js
// Phiên bản tái cấu trúc để xử lý nhiều camera ổn định
// =============================================================

// Sử dụng IIFE (Immediately Invoked Function Expression) để tạo một scope riêng,
// tránh làm ô nhiễm global scope và quản lý code tốt hơn.
(function (overlay) {

    "use strict";

    // --- CÁC BIẾN CỤC BỘ (PRIVATE) CỦA MODULE ---
    let connection;
    const hlsInstances = new Map();
    const boxBuffers = {}; // { camId: { sn: boxes } }
    const currentSnByCam = {}; // { camId: sn }

    /**
     * Hàm này chỉ được gọi MỘT LẦN DUY NHẤT bởi overlay.init().
     * Nó chịu trách nhiệm tạo kết nối SignalR và đăng ký các event handler.
     */
    function initializeSignalR() {
        if (connection) return;

        connection = new signalR.HubConnectionBuilder()
            .withUrl("/overlayHub")
            .withAutomaticReconnect()
            .build();

        // Đăng ký handler `ReceiveBoxes` MỘT LẦN.
        // Handler này sẽ nhận dữ liệu cho TẤT CẢ các camera và phân phối chúng.
        connection.on("ReceiveBoxes", (camId, sn, boxes) => {
            console.log(`📦 RX cam=${camId} sn=${sn} cur=${currentSnByCam[camId]}`);

            if (Array.isArray(boxes[0])) {
                boxes = boxes.map(a => ({
                    x: a[0], y: a[1],
                    width: a[2] - a[0],
                    height: a[3] - a[1],
                    label: "person"
                }));
            }

            if (sn === currentSnByCam[camId]) {
                drawBoxes(camId, boxes);
            } else {
                (boxBuffers[camId] ??= {})[sn] = boxes;
            }
        });

        connection.start()
            .then(() => console.log("✅ SignalR connected and ready."))
            .catch(err => console.error("❌ SignalR connection error:", err));

        // Xử lý việc join lại group sau khi mất kết nối và kết nối lại thành công
        connection.onreconnected(() => {
            console.log("SignalR reconnected. Re-joining all active groups...");
            for (const camId of hlsInstances.keys()) {
                overlay.joinGroup(camId);
            }
        });
    }

    /**
     * Chịu trách nhiệm vẽ các bounding box lên canvas.
     */
    function drawBoxes(camId, boxes) {
        requestAnimationFrame(() => {
            const video = document.getElementById(`${camId}_video`);
            const canvas = document.getElementById(`${camId}_overlay`);
            if (!video || !canvas) return;

            // Xóa canvas trước khi vẽ mới
            const ctx = canvas.getContext("2d");
            canvas.width = video.clientWidth;
            canvas.height = video.clientHeight;
            ctx.clearRect(0, 0, canvas.width, canvas.height);

            if (!boxes || boxes.length === 0) return;

            const scaleX = canvas.width / (video.videoWidth || canvas.width);
            const scaleY = canvas.height / (video.videoHeight || canvas.height);

            ctx.lineWidth = 2;
            ctx.strokeStyle = "red";
            ctx.font = "16px sans-serif";
            ctx.fillStyle = "red";

            boxes.forEach(b => {
                const x = b.x * scaleX, y = b.y * scaleY;
                const w = b.width * scaleX, h = b.height * scaleY;
                ctx.strokeRect(x, y, w, h);
                if (b.label) ctx.fillText(b.label, x + 4, y + 16);
            });
        });
    }

    // --- PUBLIC API: CÁC HÀM SẼ ĐƯỢC GỌI TỪ BÊN NGOÀI ---

    /**
     * Khởi tạo hệ thống overlay. Phải được gọi MỘT LẦN DUY NHẤT khi trang được tải.
     */
    overlay.init = function () {
        console.log("Overlay system initializing...");
        initializeSignalR();
    };

    /**
     * Khởi tạo và bắt đầu phát video cho một camera cụ thể.
     */
    overlay.startPlayer = function (camId) {
        const video = document.getElementById(`${camId}_video`);
        const canvas = document.getElementById(`${camId}_overlay`);
        if (!video || !canvas) {
            console.error(`[${camId}] elements not found`);
            return;
        }

        if (hlsInstances.has(camId)) {
            hlsInstances.get(camId).destroy();
        }

        const hls = new Hls({
            debug: false,
            enableWorker: true,
            lowLatencyMode: false,
            backBufferLength: 30,
            maxBufferLength: 6,
            maxMaxBufferLength: 12,
            liveSyncDurationCount: 1,
            liveMaxLatencyDurationCount: 5,
            maxBufferHole: 0.3
        });
        hlsInstances.set(camId, hls);

        hls.on(Hls.Events.MANIFEST_PARSED, () => {
            video.play().catch(() => {
                console.warn(`[${camId}] Autoplay prevented. Muting and retrying.`);
                video.muted = true;
                video.play();
            });
        });

        hls.on(Hls.Events.FRAG_CHANGED, (_, data) => {
            const sn = data.frag.sn;
            currentSnByCam[camId] = sn;

            const buf = boxBuffers[camId];
            if (buf && buf[sn]) {
                drawBoxes(camId, buf[sn]);
                delete buf[sn];
            } else {
                // Xóa box cũ nếu không có box mới cho segment này
                drawBoxes(camId, []);
            }
        });

        hls.on(Hls.Events.ERROR, (_, data) => {
            if (data.fatal) {
                console.error(`[${camId}] Fatal HLS error, recovering...`, data.details);
                setTimeout(() => overlay.startPlayer(camId), 3000);
            }
        });

        hls.loadSource(`/hls/${camId}/index.m3u8`);
        hls.attachMedia(video);
    };

    /**
     * Tham gia vào group SignalR để nhận dữ liệu cho một camera.
     */
    overlay.joinGroup = function (camId) {
        if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
            console.warn(`[${camId}] SignalR not ready, trying to join group again in 100ms.`);
            setTimeout(() => overlay.joinGroup(camId), 100);
            return;
        }
        connection.invoke("JoinCameraGroup", camId)
            .then(() => console.log(`📡 Joined SignalR group: ${camId}`))
            .catch(err => console.error(`Failed to join group ${camId}`, err));
    };

})(window.overlay = window.overlay || {});

//// wwwroot/js/overlay.js
//// Phiên bản tái cấu trúc hoàn chỉnh, ổn định cho nhiều camera
//// =============================================================

//// Sử dụng IIFE (Immediately Invoked Function Expression) để tạo một scope riêng,
//// tránh làm ô nhiễm global scope và quản lý code tốt hơn.
//(function (overlay) {
//    "use strict";

//    // --- CÁC BIẾN CỤC BỘ (PRIVATE) CỦA MODULE ---
//    const BOX_TIMEOUT_MS = 2000; // Hộp sẽ biến mất sau 2 giây
//    let connection;
//    const hlsInstances = new Map();
//    const boxBuffers = {};

//    /**
//     * Hàm này chỉ được gọi MỘT LẦN DUY NHẤT bởi overlay.init().
//     * Nó chịu trách nhiệm tạo kết nối SignalR và đăng ký các event handler.
//     */
//    function initializeSignalR() {
//        if (connection) return;

//        connection = new signalR.HubConnectionBuilder()
//            .withUrl("/overlayHub")
//            .withAutomaticReconnect()
//            .build();

//        // Đăng ký handler `ReceiveBoxes` MỘT LẦN.
//        // Handler này sẽ nhận dữ liệu cho TẤT CẢ các camera và phân phối chúng.
//        connection.on("ReceiveBoxes", (camId, sn, boxes) => {
//            // Log chi tiết để debug
//            console.log(`[${camId}] Received boxes for segment: ${sn}`);
//            if (!boxBuffers[camId]) {
//                boxBuffers[camId] = {};
//            }
//            boxBuffers[camId][sn] = boxes;
//        });

//        connection.start()
//            .then(() => console.log("✅ SignalR connected and ready."))
//            .catch(err => console.error("❌ SignalR connection error:", err));

//        // Xử lý việc join lại group sau khi mất kết nối và kết nối lại thành công
//        connection.onreconnected(() => {
//            console.log("SignalR reconnected. Re-joining all active groups...");
//            // Lặp qua tất cả các camera đang chạy và join lại group của chúng
//            for (const camId of hlsInstances.keys()) {
//                overlay.joinGroup(camId);
//            }
//        });
//    }

//    /**
//     * Vòng lặp vẽ chính, chạy 60 lần/giây để đồng bộ và vẽ bounding box.
//     */
//    function drawLoop(camId, video, canvas) {
//        if (!video || video.paused || video.seeking || !boxBuffers[camId]) {
//            requestAnimationFrame(() => drawLoop(camId, video, canvas));
//            return;
//        }

//        const videoTimeMs = video.currentTime * 1000;
//        const buffer = boxBuffers[camId];

//        let latestBoxInfo = null;
//        for (let i = buffer.length - 1; i >= 0; i--) {
//            if (buffer[i].ts <= videoTimeMs) {
//                latestBoxInfo = buffer[i];
//                break;
//            }
//        }

//        let boxesToDraw = [];
//        if (latestBoxInfo && (videoTimeMs - latestBoxInfo.ts < BOX_TIMEOUT_MS)) {
//            boxesToDraw = latestBoxInfo.boxes;
//        }

//        drawBoxes(canvas, video, boxesToDraw);
//        requestAnimationFrame(() => drawLoop(camId, video, canvas));
//    }

//    /**
//     * Chịu trách nhiệm vẽ các bounding box lên canvas.
//     */
//    function drawBoxes(canvas, video, boxes) {
//        if (!video.videoWidth || !video.videoHeight) {
//            const ctx = canvas.getContext("2d");
//            ctx.clearRect(0, 0, canvas.width, canvas.height);
//            return;
//        }

//        canvas.width = video.clientWidth;
//        canvas.height = video.clientHeight;
//        const ctx = canvas.getContext("2d");
//        ctx.clearRect(0, 0, canvas.width, canvas.height);

//        if (!boxes || boxes.length === 0) return;

//        // Không cần quy đổi tỉ lệ nữa vì đang dùng kiến trúc segment
//        const scaleX = canvas.width / video.videoWidth;
//        const scaleY = canvas.height / video.videoHeight;

//        ctx.lineWidth = 2;
//        ctx.strokeStyle = "red";
//        ctx.font = "16px sans-serif";
//        ctx.fillStyle = "red";

//        boxes.forEach(b => {
//            const x = b.x * scaleX;
//            const y = b.y * scaleY;
//            const w = b.width * scaleX;
//            const h = b.height * scaleY;
//            ctx.strokeRect(x, y, w, h);
//            if (b.label) ctx.fillText(b.label, x + 4, y + 16);
//        });
//    }


//    // --- PUBLIC API: CÁC HÀM SẼ ĐƯỢC GỌI TỪ BÊN NGOÀI ---

//    /**
//     * Khởi tạo hệ thống overlay. Phải được gọi MỘT LẦN DUY NHẤT khi trang được tải.
//     */
//    overlay.init = function () {
//        console.log("Overlay system initializing...");
//        initializeSignalR();
//    };

//    /**
//     * Khởi tạo và bắt đầu phát video cho một camera cụ thể.
//     * @param {string} camId ID của camera.
//     */
//    overlay.startPlayer = function (camId) {
//        const video = document.getElementById(`${camId}_video`);
//        const canvas = document.getElementById(`${camId}_overlay`);
//        if (!video || !canvas) {
//            console.error(`[${camId}] video/canvas element not found`);
//            return;
//        }

//        if (hlsInstances.has(camId)) {
//            hlsInstances.get(camId).destroy();
//        }
//        video.src = '';
//        video.removeAttribute('src');
//        video.load();

//        console.log(`[${camId}] Initializing new HLS player instance.`);
//        const hls = new Hls({
//            liveSyncDurationCount: 3,
//            liveMaxLatencyDurationCount: 5,
//        });
//        hlsInstances.set(camId, hls);

//        hls.on(Hls.Events.FRAG_CHANGED, (_, data) => {
//            const sn = data.frag.sn;
//            console.log(`[${camId}] Player is now at segment: ${sn}`);

//            if (boxBuffers[camId] && boxBuffers[camId][sn]) {
//                drawBoxes(canvas, video, boxBuffers[camId][sn]);
//                delete boxBuffers[camId][sn];
//            } else {
//                drawBoxes(canvas, video, []);
//            }
//        });

//        hls.on(Hls.Events.MANIFEST_PARSED, function () {
//            video.play().catch(() => {
//                video.muted = true;
//                video.play();
//            });
//        });

//        hls.on(Hls.Events.ERROR, (_, data) => {
//            if (data.fatal) {
//                console.error(`[${camId}] Fatal HLS error, recovering...`, data.details);
//                setTimeout(() => overlay.startPlayer(camId), 3000);
//            }
//        });

//        hls.loadSource(`/hls/${camId}/index.m3u8`);
//        hls.attachMedia(video);
//    };

//    /**
//     * Tham gia vào group SignalR để nhận dữ liệu cho một camera.
//     * @param {string} camId ID của camera.
//     */
//    overlay.joinGroup = function (camId) {
//        if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
//            console.warn(`[${camId}] SignalR not ready, trying to join group again in 100ms.`);
//            setTimeout(() => overlay.joinGroup(camId), 100);
//            return;
//        }
//        connection.invoke("JoinCameraGroup", camId)
//            .then(() => console.log(`📡 Joined SignalR group: ${camId}`))
//            .catch(err => console.error(`Failed to join group ${camId}`, err));
//    };

//})(window.overlay = window.overlay || {});

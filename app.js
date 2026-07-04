const WebSocket = require('ws');
const fs = require('fs');
const path = require('path');
const { exec } = require('child_process');
const net = require('net');

const configPath = path.join(__dirname, 'config.json');
let config = { debug: false };
try {
    if (fs.existsSync(configPath)) {
        config = Object.assign(config, JSON.parse(fs.readFileSync(configPath, 'utf8')));
    }
} catch (e) {
    // 忽略配置文件读取错误
}

const logDir = path.join(__dirname, 'logs');
const logFile = path.join(logDir, 'plugin.log');

function log(msg) {
    if (!config.debug) return;
    try {
        if (!fs.existsSync(logDir)) {
            fs.mkdirSync(logDir, { recursive: true });
        }
        fs.appendFileSync(logFile, `[${new Date().toISOString()}] ${msg}\n`);
    } catch (e) {
        // 忽略日志写入错误
    }
}

log("Plugin started with args: " + process.argv.join(" "));

// Ulanzi passes arguments directly without flags: [address, port, language]
const address = process.argv[2] || '127.0.0.1';
const port = process.argv[3];
const language = process.argv[4] || 'en';
const uuid = 'com.lsby.smart-volume'; // 对应 manifest.json 中的 UUID

if (!port) {
    log("Missing port argument.");
    process.exit(1);
}

const svvPath = path.join(__dirname, 'bin', 'SoundVolumeView.exe');
const getActiveAppPath = path.join(__dirname, 'bin', 'get-active-app.exe');
const osdPath = path.join(__dirname, 'bin', 'volume-osd.exe');

function triggerVolumeCmd(targetMode, direction) {
    const step = 2;
    const change = direction.includes('right') ? `+${step}` : `-${step}`;
    
    const pipeName = '\\\\.\\pipe\\UlanziSmartVolumeOSD';
    const msg = `CMD|${targetMode}|${change}\r\n`;
    
    const client = net.connect(pipeName, () => {
        client.write(msg);
        client.end();
    });
    client.on('error', () => {
        // 如果管道不存在（程序没开），就直接用命令行拉起，并传入当前 Node.js 的 PID 用于进程绑定
        const spawn = require('child_process').spawn;
        const child = spawn(`"${osdPath}"`, ["CMD", targetMode, change, process.pid.toString()], {
            detached: true,
            shell: true,
            windowsHide: false
        });
        child.unref();
    });
}

const ws = new WebSocket(`ws://${address}:${port}`);

let lastDialDownTime = 0;

ws.on('open', () => {
    log("Connected to Ulanzi Deck WebSocket.");
    const registerJson = {
        code: 0,
        cmd: 'connected',
        uuid: uuid
    };
    ws.send(JSON.stringify(registerJson));
    
    // 静默预热 OSD 进程，绑定生命周期
    const spawn = require('child_process').spawn;
    const child = spawn(`"${osdPath}"`, ["CMD", "Preload", "0", process.pid.toString()], {
        detached: true,
        shell: true,
        windowsHide: false
    });
    child.unref();
});

ws.on('message', (data) => {
    try {
        const msg = JSON.parse(data.toString());
        if (msg.cmd === 'dialrotate') {
            if (msg.rotateEvent.includes('hold')) {
                log(`[ACTION] 旋钮按下时扭动: 方向=${msg.rotateEvent}`);
                triggerVolumeCmd('Foreground', msg.rotateEvent);
            } else {
                log(`[ACTION] 旋钮正常扭动: 方向=${msg.rotateEvent}`);
                triggerVolumeCmd('Master', msg.rotateEvent);
            }
        } else if (msg.cmd === 'dialdown') {
            const now = Date.now();
            log(`[ACTION] 旋钮被按下`);
            if (now - lastDialDownTime < 400) {
                log(`[ACTION] 旋钮双击`);
                triggerVolumeCmd('ToggleListMode', 'right');
                lastDialDownTime = 0;
            } else {
                lastDialDownTime = now;
                log(`[ACTION] 旋钮单击`);
                triggerVolumeCmd('SingleClick', 'right');
            }
        } else if (msg.cmd === 'dialup') {
            log(`[ACTION] 旋钮被松开`);
        }
    } catch (e) {
        log("Error parsing message: " + e.message);
    }
});

ws.on('close', () => {
    log("WebSocket closed.");
    process.exit(0);
});

ws.on('error', (err) => {
    log("WebSocket error: " + err.message);
});

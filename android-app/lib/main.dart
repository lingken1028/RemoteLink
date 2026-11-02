import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';
import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:http/http.dart' as http;

void main() => runApp(const RemoteLinkApp());

class RemoteLinkApp extends StatelessWidget {
  const RemoteLinkApp({super.key});

  @override
  Widget build(BuildContext context) => MaterialApp(
        title: 'RemoteLink',
        debugShowCheckedModeBanner: false,
        theme: ThemeData(
          brightness: Brightness.dark,
          colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF36B89A), brightness: Brightness.dark),
          scaffoldBackgroundColor: const Color(0xFF101827),
          useMaterial3: true,
        ),
        home: const PairScreen(),
      );
}

class PairScreen extends StatefulWidget {
  const PairScreen({super.key});
  @override
  State<PairScreen> createState() => _PairScreenState();
}

class _PairScreenState extends State<PairScreen> {
  static const _storage = FlutterSecureStorage();
  final _hostController = TextEditingController();
  final _codeController = TextEditingController();
  String _message = '請先在 Windows 主機啟動配對服務，再輸入主機 IP 與配對碼。';
  bool _busy = false;
  bool _paired = false;
  String? _token;
  String? _baseUrl;

  @override
  void initState() {
    super.initState();
    _restoreSession();
  }

  Future<void> _restoreSession() async {
    final host = await _storage.read(key: 'host');
    final token = await _storage.read(key: 'token');
    if (!mounted) return;
    if (host != null && token != null) {
      setState(() {
        _hostController.text = host;
        _baseUrl = _normalizeHost(host);
        _token = token;
        _paired = true;
        _message = '已保存配對資訊。按「檢查連線」驗證主機是否仍在線。';
      });
    }
  }

  String _normalizeHost(String input) {
    var value = input.trim();
    if (!value.startsWith('http://') && !value.startsWith('https://')) value = 'http://$value';
    if (!value.contains(':', value.indexOf('://') + 3)) value = '$value:43821';
    return value.endsWith('/') ? value.substring(0, value.length - 1) : value;
  }

  Future<void> _pair() async {
    final code = _codeController.text.trim();
    if (code.length != 6 || int.tryParse(code) == null) {
      setState(() => _message = '請輸入 6 位數字配對碼。');
      return;
    }
    setState(() { _busy = true; _message = '正在向 Windows 主機驗證配對碼…'; });
    final base = _normalizeHost(_hostController.text);
    try {
      final response = await http.post(
        Uri.parse('$base/pair'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({'code': code, 'clientName': 'Xiaomi 15 Pro'}),
      ).timeout(const Duration(seconds: 8));
      if (response.statusCode == 200) {
        final data = jsonDecode(response.body) as Map<String, dynamic>;
        final token = data['token'] as String;
        await _storage.write(key: 'host', value: _hostController.text.trim());
        await _storage.write(key: 'token', value: token);
        if (!mounted) return;
        setState(() {
          _baseUrl = base; _token = token; _paired = true;
          _message = '配對成功，已取得此主機的工作階段授權。';
        });
      } else {
        final data = jsonDecode(response.body);
        setState(() => _message = '配對失敗（HTTP ${response.statusCode}）：${data is Map ? data['message'] ?? data['error'] ?? '請確認主機位址與配對碼' : '請確認主機位址與配對碼'}');
      }
    } catch (e) {
      setState(() => _message = '無法連接主機：$e\n請確認手機與電腦位於同一 Wi-Fi，並允許 Windows 防火牆通過 TCP 43821。');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _checkSession() async {
    if (_baseUrl == null || _token == null) return;
    setState(() { _busy = true; _message = '正在檢查授權…'; });
    try {
      final r = await http.get(Uri.parse('$_baseUrl/session'), headers: {'Authorization': 'Bearer $_token'}).timeout(const Duration(seconds: 8));
      setState(() => _message = r.statusCode == 200 ? '主機在線，配對授權有效。' : '授權已失效或主機已停止（HTTP ${r.statusCode}）。');
    } catch (e) {
      setState(() => _message = '檢查失敗：$e');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _disconnect() async {
    if (_baseUrl != null && _token != null) {
      try {
        await http.post(Uri.parse('$_baseUrl/session/close'), headers: {'Authorization': 'Bearer $_token'}).timeout(const Duration(seconds: 5));
      } catch (_) {}
    }
    await _storage.delete(key: 'token');
    await _storage.delete(key: 'host');
    if (!mounted) return;
    setState(() {
      _paired = false; _token = null; _baseUrl = null;
      _message = '已清除此裝置保存的配對授權。';
    });
  }

  void _openPreview() {
    if (_baseUrl == null || _token == null) return;
    Navigator.of(context).push(MaterialPageRoute(
      builder: (_) => DesktopPreviewPage(baseUrl: _baseUrl!, token: _token!),
    ));
  }

  @override
  void dispose() {
    _hostController.dispose();
    _codeController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('RemoteLink')),
        body: SafeArea(
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 460),
              child: SingleChildScrollView(
                padding: const EdgeInsets.all(24),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const Icon(Icons.desktop_windows_rounded, size: 68, color: Color(0xFF71D5B3)),
                    const SizedBox(height: 18),
                    const Text('連接 Windows 主機', textAlign: TextAlign.center, style: TextStyle(fontSize: 24, fontWeight: FontWeight.bold)),
                    const SizedBox(height: 8),
                    const Text('Windows 11 · Xiaomi 15 Pro', textAlign: TextAlign.center, style: TextStyle(color: Colors.white60)),
                    const SizedBox(height: 28),
                    TextField(controller: _hostController, keyboardType: TextInputType.url, decoration: const InputDecoration(labelText: 'Windows 主機 IP（例如 192.168.1.20）', border: OutlineInputBorder())),
                    const SizedBox(height: 14),
                    if (!_paired) TextField(
                      controller: _codeController,
                      keyboardType: TextInputType.number,
                      maxLength: 6,
                      textAlign: TextAlign.center,
                      style: const TextStyle(fontSize: 28, letterSpacing: 8),
                      decoration: const InputDecoration(labelText: '6 位數配對碼', border: OutlineInputBorder(), counterText: ''),
                    ),
                    const SizedBox(height: 16),
                    FilledButton(
                      onPressed: _busy ? null : (_paired ? _checkSession : _pair),
                      child: Padding(
                        padding: const EdgeInsets.all(13),
                        child: _busy ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2)) : Text(_paired ? '檢查連線' : '配對主機'),
                      ),
                    ),
                    if (_paired) OutlinedButton.icon(
                      onPressed: _busy ? null : _openPreview,
                      icon: const Icon(Icons.desktop_windows),
                      label: const Text('查看 Windows 桌面'),
                    ),
                    if (_paired) OutlinedButton(onPressed: _busy ? null : _disconnect, child: const Text('解除配對')),
                    const SizedBox(height: 18),
                    Text(_message, textAlign: TextAlign.center, style: const TextStyle(color: Colors.white70, height: 1.5)),
                    const SizedBox(height: 20),
                    const Text('桌面預覽需先在 Windows 主機按「允許桌面預覽」。目前為區域網路 JPEG 預覽原型，尚未支援即時影音、滑鼠鍵盤控制或跨網路連線。', textAlign: TextAlign.center, style: TextStyle(color: Colors.white38, fontSize: 12)),
                  ],
                ),
              ),
            ),
          ),
        ),
      );
}

class DesktopPreviewPage extends StatefulWidget {
  const DesktopPreviewPage({super.key, required this.baseUrl, required this.token});
  final String baseUrl;
  final String token;

  @override
  State<DesktopPreviewPage> createState() => _DesktopPreviewPageState();
}

class _DesktopPreviewPageState extends State<DesktopPreviewPage> {
  Timer? _timer;
  final TransformationController _transformController = TransformationController();
  Uint8List? _frame;
  bool _controlMode = false;
  int _screenWidth = 1920;
  int _screenHeight = 1080;
  DateTime _lastMouseMove = DateTime.fromMillisecondsSinceEpoch(0);

  Future<void> _loadDesktopInfo() async {
    try {
      final response = await http.get(
        Uri.parse('${widget.baseUrl}/desktop/info'),
        headers: {'Authorization': 'Bearer ${widget.token}'},
      ).timeout(const Duration(seconds: 5));
      if (response.statusCode == 200 && mounted) {
        final data = jsonDecode(response.body) as Map<String, dynamic>;
        setState(() {
          _screenWidth = (data['width'] as num?)?.toInt() ?? 1920;
          _screenHeight = (data['height'] as num?)?.toInt() ?? 1080;
        });
      }
    } catch (_) {}
  }

  Future<void> _sendMouse(Offset position, Size size, String action) async {
    if (size.width <= 0 || size.height <= 0) return;
    final x = (position.dx / size.width).clamp(0.0, 1.0);
    final y = (position.dy / size.height).clamp(0.0, 1.0);
    try {
      await http.post(
        Uri.parse('${widget.baseUrl}/input/mouse'),
        headers: {
          'Authorization': 'Bearer ${widget.token}',
          'Content-Type': 'application/json',
        },
        body: jsonEncode({'action': action, 'x': x, 'y': y}),
      ).timeout(const Duration(seconds: 3));
    } catch (_) {
      if (mounted) setState(() => _status = '滑鼠指令傳送失敗');
    }
  }

  void _zoomBy(double factor) {
    final currentScale = _transformController.value.getMaxScaleOnAxis();
    final targetScale = (currentScale * factor).clamp(1.0, 4.0);
    _transformController.value = Matrix4.identity()..scale(targetScale);
  }

  void _resetZoom() {
    _transformController.value = Matrix4.identity();
  }
  String _status = '正在連接桌面預覽…';
  bool _loading = false;
  DateTime? _lastFrame;

  @override
  void initState() {
    super.initState();
    _loadDesktopInfo();
    _loadFrame();
    _timer = Timer.periodic(const Duration(seconds: 1), (_) => _loadFrame());
  }

  Future<void> _loadFrame() async {
    if (_loading || !mounted) return;
    _loading = true;
    try {
      final response = await http.get(
        Uri.parse('${widget.baseUrl}/desktop.jpg'),
        headers: {'Authorization': 'Bearer ${widget.token}'},
      ).timeout(const Duration(seconds: 5));
      if (response.statusCode == 200 && response.headers['content-type']?.contains('image/jpeg') == true) {
        if (!mounted) return;
        setState(() {
          _frame = response.bodyBytes;
          _lastFrame = DateTime.now();
          _status = '已接收桌面畫面 · 原型約 1 FPS';
        });
      } else {
        String message = 'HTTP ${response.statusCode}';
        try {
          final body = jsonDecode(response.body);
          if (body is Map && body['message'] != null) message = body['message'].toString();
        } catch (_) {}
        if (mounted) setState(() => _status = message);
      }
    } catch (e) {
      if (mounted) setState(() => _status = '影像接收失敗：$e');
    } finally {
      _loading = false;
    }
  }

  @override
  void dispose() {
    _timer?.cancel();
    _transformController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(
          title: const Text('Windows 桌面預覽'),
          actions: [
            IconButton(
              tooltip: _controlMode ? '切換到檢視模式' : '切換到操作模式',
              onPressed: () => setState(() => _controlMode = !_controlMode),
              icon: Icon(_controlMode ? Icons.pan_tool_alt : Icons.mouse),
            ),
            if (!_controlMode) IconButton(
              tooltip: '縮小',
              onPressed: () => _zoomBy(0.8),
              icon: const Icon(Icons.zoom_out),
            ),
            if (!_controlMode) IconButton(
              tooltip: '放大',
              onPressed: () => _zoomBy(1.25),
              icon: const Icon(Icons.zoom_in),
            ),
            if (!_controlMode) IconButton(
              tooltip: '重設縮放',
              onPressed: _resetZoom,
              icon: const Icon(Icons.fit_screen),
            ),
          ],
        ),
        body: Column(
          children: [
            Expanded(
              child: Container(
                width: double.infinity,
                color: Colors.black,
                alignment: Alignment.center,
                child: _frame == null
                    ? const CircularProgressIndicator()
                    : _controlMode
                        ? Center(
                            child: AspectRatio(
                              aspectRatio: _screenWidth / _screenHeight,
                              child: LayoutBuilder(
                                builder: (context, constraints) => GestureDetector(
                                  behavior: HitTestBehavior.opaque,
                                  onTapUp: (details) => _sendMouse(details.localPosition, constraints.biggest, 'click'),
                                  onLongPressStart: (details) => _sendMouse(details.localPosition, constraints.biggest, 'rightClick'),
                                  onPanUpdate: (details) {
                                    final now = DateTime.now();
                                    if (now.difference(_lastMouseMove).inMilliseconds >= 70) {
                                      _lastMouseMove = now;
                                      _sendMouse(details.localPosition, constraints.biggest, 'move');
                                    }
                                  },
                                  child: Image.memory(_frame!, fit: BoxFit.fill, gaplessPlayback: true),
                                ),
                              ),
                            ),
                          )
                        : LayoutBuilder(
                            builder: (context, constraints) => InteractiveViewer(
                              transformationController: _transformController,
                              minScale: 1.0,
                              maxScale: 4.0,
                              panEnabled: true,
                              scaleEnabled: true,
                              boundaryMargin: const EdgeInsets.all(120),
                              child: SizedBox(
                                width: constraints.maxWidth,
                                height: constraints.maxHeight,
                                child: Image.memory(
                                  _frame!,
                                  fit: BoxFit.contain,
                                  gaplessPlayback: true,
                                ),
                              ),
                            ),
                          ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.all(14),
              child: Column(
                children: [
                  Text(_controlMode ? '操作模式：點擊＝左鍵 · 長按＝右鍵 · 拖曳＝移動滑鼠' : _status, textAlign: TextAlign.center, style: const TextStyle(color: Colors.white70)),
                  if (_lastFrame != null)
                    Text('最後影格：${TimeOfDay.fromDateTime(_lastFrame!).format(context)}', style: const TextStyle(color: Colors.white38, fontSize: 12)),
                  const SizedBox(height: 4),
                  const Text('這是低更新率 LAN 預覽測試，不是 WebRTC 即時串流。', style: TextStyle(color: Colors.white38, fontSize: 11)),
                ],
              ),
            ),
          ],
        ),
      );
}

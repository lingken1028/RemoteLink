import 'package:flutter/material.dart';

void main() => runApp(const RemoteLinkApp());

class RemoteLinkApp extends StatelessWidget {
  const RemoteLinkApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
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
}

class PairScreen extends StatefulWidget {
  const PairScreen({super.key});

  @override
  State<PairScreen> createState() => _PairScreenState();
}

class _PairScreenState extends State<PairScreen> {
  final _codeController = TextEditingController();
  String _message = '輸入 Windows 主機顯示的配對碼以開始配對。';
  bool _busy = false;

  @override
  void dispose() {
    _codeController.dispose();
    super.dispose();
  }

  Future<void> _pair() async {
    final code = _codeController.text.trim();
    if (code.length != 6 || int.tryParse(code) == null) {
      setState(() => _message = '請輸入 6 位數字配對碼。');
      return;
    }
    setState(() {
      _busy = true;
      _message = '配對服務尚未接通。此畫面目前僅供 UI 與 APK 建置驗證。';
    });
    await Future<void>.delayed(const Duration(milliseconds: 350));
    if (mounted) setState(() => _busy = false);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('RemoteLink')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 440),
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const Icon(Icons.desktop_windows_rounded, size: 68, color: Color(0xFF71D5B3)),
                  const SizedBox(height: 22),
                  const Text('連接 Windows 主機', textAlign: TextAlign.center,
                      style: TextStyle(fontSize: 24, fontWeight: FontWeight.bold)),
                  const SizedBox(height: 10),
                  const Text('Windows 11 · Xiaomi 15 Pro', textAlign: TextAlign.center,
                      style: TextStyle(color: Colors.white60)),
                  const SizedBox(height: 30),
                  TextField(
                    controller: _codeController,
                    keyboardType: TextInputType.number,
                    maxLength: 6,
                    textAlign: TextAlign.center,
                    style: const TextStyle(fontSize: 28, letterSpacing: 8),
                    decoration: const InputDecoration(
                      labelText: '6 位數配對碼',
                      border: OutlineInputBorder(),
                      counterText: '',
                    ),
                  ),
                  const SizedBox(height: 16),
                  FilledButton(
                    onPressed: _busy ? null : _pair,
                    child: Padding(
                      padding: const EdgeInsets.all(13),
                      child: _busy
                          ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                          : const Text('配對主機'),
                    ),
                  ),
                  const SizedBox(height: 18),
                  Text(_message, textAlign: TextAlign.center,
                      style: const TextStyle(color: Colors.white70, height: 1.5)),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

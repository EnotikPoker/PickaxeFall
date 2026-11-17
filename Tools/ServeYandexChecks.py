#!/usr/bin/env python3
"""Local Web build host. --mock-sdk is isolated test infrastructure, never an archive asset."""
import argparse,http.server,json
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('directory',type=Path);p.add_argument('--port',type=int,default=8093);p.add_argument('--mock-sdk',action='store_true');a=p.parse_args()
class Handler(http.server.SimpleHTTPRequestHandler):
 def __init__(self,*args,**kw):super().__init__(*args,directory=str(a.directory.resolve()),**kw)
 def do_GET(self):
  if self.path.split('?')[0]=='/sdk.js' and a.mock_sdk:
   content=Path(__file__).with_name('YandexSdkTestDouble.js').read_bytes();self.send_response(200);self.send_header('Content-Type','application/javascript');self.send_header('Content-Length',str(len(content)));self.end_headers();self.wfile.write(content);return
  super().do_GET()
 def log_message(self,*args):pass
print('Serving local build on port',a.port,'with SDK double' if a.mock_sdk else 'without SDK',flush=True)
http.server.ThreadingHTTPServer(('127.0.0.1',a.port),Handler).serve_forever()

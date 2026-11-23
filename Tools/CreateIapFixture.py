"""Ephemeral, self-signed purchase fixtures for a separate QA application only."""
import base64,json,subprocess,tempfile
from pathlib import Path

def create(output):
    with tempfile.TemporaryDirectory() as temp:
        root=Path(temp);key=root/'test.pem'
        subprocess.run(['openssl','genpkey','-algorithm','RSA','-pkeyopt','rsa_keygen_bits:2048','-out',str(key)],check=True,stderr=subprocess.DEVNULL)
        public=subprocess.check_output(['openssl','pkey','-in',str(key),'-pubout','-outform','DER'],stderr=subprocess.DEVNULL)
        fixtures=[]
        for state,product,package,token in [(0,'remove_ads','com.enotikpoker.pickaxefall.playqa','test-token'),(1,'remove_ads','com.enotikpoker.pickaxefall.playqa','test-token'),(2,'remove_ads','com.enotikpoker.pickaxefall.playqa','test-token'),(4,'remove_ads','com.enotikpoker.pickaxefall.playqa','test-token'),(0,'coins','com.enotikpoker.pickaxefall.playqa','test-token'),(0,'remove_ads','other.package','test-token'),(0,'remove_ads','com.enotikpoker.pickaxefall.playqa','')]:
            data=json.dumps({'orderId':'QA-NOT-A-REAL-ORDER','packageName':package,'productId':product,'purchaseToken':token,'purchaseTime':1700000000000,'purchaseState':state},separators=(',',':'))
            signature=subprocess.check_output(['openssl','dgst','-sha1','-sign',str(key)],input=data.encode())
            receipt={'Store':'GooglePlay','TransactionID':'QA-NOT-A-REAL-ORDER','Payload':json.dumps({'json':data,'signature':base64.b64encode(signature).decode()})}
            fixtures.append(json.dumps(receipt))
        output.write_text(json.dumps({'publicKey':base64.b64encode(public).decode(),'receipts':fixtures}))

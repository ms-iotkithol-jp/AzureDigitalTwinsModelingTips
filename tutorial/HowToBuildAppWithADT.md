# Azure Digital Twins を利用した、アプリケーション開発  
開発するソリューションが扱う概念群の今の状態（Twin Graph）を保持するのが Azure Digital Twins であり、Twin・Relationship の生成・削除、Twin Property の参照・更新、生成・削除・更新をトリガーにしたメッセージや Telemetry の発信等、ビジネスユースケースの実装は、アプリケーションロジックが担当する。  
本チュートリアルでは以下の様な構成の アプリケーションロジック群を作成する。  
![solution architecture](images/solution-architecture.svg)

## WPF Application  
ビジネスシナリオに沿った、Twin の生成、Twin Property 更新、Twin 間の Relationship 生成・削除、Relationship の利用も含めた Twin の検索といった操作を、User On Demand で行う。  
⇒ [HowToBuildWPFApp.md](./HowToBuildWPFApp.md) 

IoT Hub を通じて、デバイスの世界からのテレメトリーデータを供給する、シミュレーションアプリケーション。  
⇒ [Azure Digital Twins にテレメトリーデータを供給するためのシミュレータ開発](../samples/wpfapp/WpfAppProductTransportSample/WpfAppDliverTruckDriverMobileSimulator)
このシミュレータを使って、Cooling Container Truck、Delivery Truck のモバイル端末から送られるテレメトリーデータを Azure IoT Hub に送信する。
## Function Applications  
Azure Digital Twins にアクセスする Function の作り方は、[HowToBuildFunctionApps.md](./HowToBuildFunctionApps.md) を参照の事。  
このチュートリアルでは、以下の Function を順番に作成していく。  

### Transfer messages from Azure IoT Hub to Azure Digital Twins  
Azure IoT Hub が受信したデータを元に、Azure Digital Twins への Telemetry Data の Publish、及び、Twin Graph の更新を行う。  
⇒[HowToConnectIoTHubToADT.md](./HowToConnectIoTHubToADT.md)  

### Propagate Consistency Twin Graph  
Azure IoT Hub が受信したデータによる Twin Graph の変更、Telemetry Data の Publish、及び、WPF アプリケーションによる Twin Graph の更新をトリガーに、関連する Twin Properties の更新と、外部アプリや他のサービスに情報を提供のための、Event Hub へのメッセージ送信を行う Function 
⇒[HowToBuildPropagateConsistencyTwinGraph.md](./HowToBuildPropagateConsistencyTwinGraph.md)  

### Catch Twin Telemetry  
Azure Digital Twins 側の Telemetry Data をキャプチャして別のサービス（例えば、Time Series Insgightsとか）に送信するための Function  
⇒[HowToBuildCatchTwinTelemetry.md](./HowToBuildCatchTwinTelemetry.md)  

### Send To Signal R  
Propagate Consistency Twin Graph function が Event Hub に送信したメッセージを受信して、SignalR サービスに通知する Function  
⇒[HowToBuildSendToSignalR.md](./HowToBuildSendToSignalR.md)  

Azure IoT サービス群を使ってソリューションを構築する際によく使うパターンを網羅してみた。  

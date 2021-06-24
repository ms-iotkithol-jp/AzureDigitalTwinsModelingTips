# Simulator Application for Cooling Container Truck and Delivery Truck  
このハンズオンに沿った形で、Azure IoT Hub → Azure Digital Twins に、運送用トラックの庫内温度、位置情報、及び、配送用トラックの位置情報と積載された温度計測機器からの温度情報を送付するシミュレーターをサンプルとして紹介する。  

## アプリ開発にあたっての基本方針  
IoT Hub に登録されたデバイスと、Azure Digital Twins の Twin Graph との紐づけは、Cooling Container Truck と Delivery Truck の "iothub_deviceid" プロパティに、IoT Hub に登録された対応するデバイスの DeviceId を保持することによって行うものとする。  
本コンテンツ作成過程において、IoT Hub 側に登録されたデバイスの Tag に、Twin の $dtId の値を格納しようと考えたが、  
- どちらがより変化しやすいか  
- IoT Hub に登録されたデバイスが送信するデータは、他のソリューションでも利用される可能性がある  
この2点から、Twin Graph 側で保持することとした。  


---
## サンプルアプリの基本機能  
サンプルアプリは以下の機能を有する。  
- Twin Graph 上の、Cooling Container Truck、Delivery Truck の情報を収集し、リスト表示  
- リストされた中から、シミュレーションする Twin を選択  
- 選択された Twin のプロパティ情報を元に、IoT Hub に登録された対応するデバイスの接続情報を取得  
- Cooling Container Truck の場合は、指定された時間間隔で、位置情報を IoT Hub に送信  
- Delivery Truck の場合は、トラックの位置情報、状態を、Twin Graph 上で搭載された製品に装備された温度計測機器群（Twin Model の定義から製品は複数搭載されているので、温度計測機器も複数ある）のそれぞれの温度、バッテリーレベル、計測時間を IoT Hub に送信  

---
## 準備  
本シミュレーターアプリを使うにあたり、以下の二つの作業を行う。  
- IoT Hub に、Twin Graph 上で定義された、Cooling Container Truck、Delivery Truck に対応するデバイスを登録する  
- Twin Graph 上の、Cooling Container Truck、Delivery Truck の Twin の "iothub_deviceid" プロパティに対応する IoT Hub 上の DeviceId をセットする  

### IoT Hub へのデバイス登録
https://docs.microsoft.com/ja-jp/azure/iot-hub/quickstart-control-device-dotnet#create-an-iot-hub の説明を参考に、Azure IoT Hub インスタンスを作成する。  

作成した Azure IoT Hub インスタンスを、https://portal.azure.com で開き、以下の様に、"IoT devices" を選択し、"+New" をクリックし、予め決めておいた Device Id を入力して、"Save" をクリックして作成する。  
![add device](../../images/sim-add-device.svg)  

例えば、Twin Graph が図の様な状態であれば、  
![current twin grap](../../images/sim-twin-graph-cct-dt.svg)  
Cooling Container Truck が2つ、Delivery Truck が2つあるので、以下の様に対応する4つの IoT Device を IoT Hub に登録する。
※ 文字列はあくまでも例なのでこの通りでなくてよい  
|Twin Graph|Type|IoT Hub|
|-|-|-|
|cct-001|Cooling Container Truck|adt-sample-cctruck-001|
|cct-002|Cooling Container Truck|adt-sample-cctruck-002|
|Shinagawa-001|Delivery Truck|adt-sample-dtruck-001|
|Ueno-001|Delivery Truck|adt-sample-dtruck-002|

次に、登録した Device Id を、Twin Graph 側の Twin のプロパティにセットする。  

![set device id](../../images/sim-set-device-id.svg)  

---
### Application への接続・認証情報設定  
Visual Studio 2019 で、[WpfAppTruckSimlator.csproj](./WpfAppTruckSimulator.csproj) を開き、 [appsettings.json](./appsettings.json) を編集する。  
```json
{
  "iothub-connetion-string": "<- IoT Hub Connection String - registry Read ->",
  "adt-instance-url": "<- your Azure Digital Twins Instance URL ->"
}
```  
<b><i>&lt;- IoT Hub Connection String - registry Read -&gt;</i></b> の部分は、以下の図に従って、Azure Portal で Azure IoT Hub の接続ポリシー情報から取得した文字列で置き換える。  

![get iot hub cs](../../images/sim-get-iothub-cs.svg)  
※ 本アプリケーションでは、Twin Graph の情報から取得した、IoT Hub の IoT device の登録情報を取得するので、 Registry Read 権限が必要。  

<b><i>&lt;- your Azure Digital Twins Instance URL -&gt;</i></b> の部分は、Azure Digital Twins の URL で置き換える。  

---
## Application の実行  
Visual Studio で Debug 実行する。  
appsettings.json に記載した接続情報がセットされた状態でウィンドウが開く。  
図の様に、2つの "Connect" ボタンを順にクリックして、作業を開始する。  
![connect adt and iot hub](../../images/sim-connect-adt-iothub.svg)  

### Cooling Container Truck の Simulation
"Get CC Trucks" をクリックすると、Azure Digital Twins の Twin Graph から Cooling Container Truck の Twin がリストアップされ、表示される。  
![get cc trucks](../../images/sim-get-cctrucks.svg)  

表示された Cooling Container Truck から Simulation したいものを選択し、"Start Simulation" をクリックすると、Simulation 用のウインドウが表示される。  
![start cc truck simulation](../../images/sim-start-cc-truck-simulation.svg)  
この際、IoT Hub のデバイスレジストリから、Cooling Container Truck の "iothub_deviceid" に保持された Device Id の接続情報を取得する。  

"Connect" をクリックすると、Azure IoT Hub に、選択した Cooling Container Truck に対応するデバイスとして接続が完了する。  
![start sending](../../images/sim-start-cc-truck-sending.svg)  
"Send Start"ボタンが有効になるのでクリックするとメッセージ送信が開始される。   
![send evidence](../../images/sim-evidence-cctruck-sending.svg)  

送信中に、Latitude、Longitude 等の値を変えると、変更された値が IoT Hub に送信される。  
※ 将来的にはこのウィンドウの右側で地図表示して、位置を変えられるようにする予定

"Send Stop" をクリックすると、送信が停止する。  

### Delivery Truck と Temperature Measurement Device の Simulation  
"Get Deliver Trucks をクリックすると、Azure Digital Twins の Twin Graph から Delivery Truck の Twin がリストアップされ、表示される。  
![get d trucks](../../images/sim-get-dtrucks.svg)  

表示された Delivery Truck から Simulation したいものを選択し、"Start Simulation" をクリックすると、Simulation 用のウインドウが表示される。  
![start d truck simulation](../../images/sim-start-d-truck-simulation.svg)  
この際、選択した Delivery Truck の "iothub_deviceid" プロパティを元に、IoT Hub の Device Registry から対応する IoT Device の接続情報を取得し、更に、Azure Digital Twins の Twin Graph から選択された Delivery Truck に、"assigned_to" で関連付けられた Temperature Measurement Device を収集する。

図では、二つの TMD が表示されている。これは、Azure Digital Twins の Twin Graph が以下の様な状態で実行しているからである。  
![adt dtruck product tmd](../../images/sim-adt-dtruck-product-tmd.svg)  
選択した Delivery Truck には、2つの TMD が関連付けられているので、このような結果になる。  

"Connect" をクリックすると、IoT Hub に接続が行われ、"Send Start" をクリックすると、Delivery Truck の位置、状態、TMDの温度、バッテリーレベルが送信される。  
![start d-truck sending](../../images/sim-start-d-truck-sending.svg)  

（ちょっと凝りすぎ？）シナリオ的に、Delivery Truck で製品を運ぶときは、クーラーボックスに製品を入れて配送するとなっているので、当然段々と温度は上昇するよなと、更に、TMD の電池もデータを送信するたびに減るよな、ということで、値の変化も気持ちシミュレーションしている。  
Delivery Truck の位置情報は、ウインドウの右側で地図表示で返られたらいいなと思っている。  

参考までに送信データを下図に示す。  
![dtruck sending data](../../images/sim-dtruck-sending-data.svg)

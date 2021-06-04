# Tutorial  
Simple すぎるシナリオだと理解が深まらない。かといって、複雑すぎると訳が分からなくなって理解が深まらない。  
適当なレベルの、在りそうなシナリオを元に、Azure Digital Twins、Azure IoT Hub、及び、関連する諸サービスの使い方を学習する。  
尚、モデリングには正解はなく、シナリオに応じて変わりうるもの（実際に使ってみて問題があれば修正すればよい）なので、ここで示すモデルは、あくまでも一つの参考例として公開している。  

## Scenario  
温度管理が必要な製品の顧客からの発注、生産、配送、顧客の受け取りを管理するシステムを開発する。  
最低限の要件は以下の通り。  
1. 温度管理が必要な製品がある。その製品は清算されてから顧客に届くまで、決められた温度範囲に保たれなければならない。  
1. 顧客に対し、発注した製品が製造中なのか、発送後はどこにあるか、温度は保たれているかを知らせることができる  
1. その製品は、顧客からの受注で工場で生産する。生産された製品は一旦、顧客の近辺の集配場に運ばれ、その集配場から顧客の元に届けられる。  
1. 工場と集配場の間の運搬は温度管理が可能なコンテナが装備されたトラックで行われる。コンテナ内の温度はトラックに装備された装置でモニターできる。  
1. 集配場から顧客までの運搬は温度計測機器とともに保温ボックスに入れられて運ばれる。運搬は温度管理機能は装備されていないトラックで行われる。温度計測機器は、ドライバーが持つモバイル端末と通信し、現在の温度をモニターできる。  
1. 顧客に届けた時点で温度計測機器は回収され次の運搬で利用される。  
1. トラックにはGPS等を元にした位置検知機器が装備されている。  
1. トラックの位置、コンテナ、及び、保温ボックスの温度は無線回線で遠隔からモニタリング可能である。  

![scenario](images/scenario.svg)

---
## Modeling  
シナリオを元に、Twin Class と Relationship 抽出し、Digital Twin Model を作成する。    
1. 開発するシステムの観点から、注目しなければならないエンティティをリストアップ。  
1. 開発するシステムの観点から、注目しなければならないエンティティ間の Relationship をリストアップ。  
1. それぞれのエンティティの識別子、共通 Propertyを抽出し、Twin Class を定義。  
1. Twin Class 間の Relationship を定義。  
1. Twin Class 毎に、Relationship も含んだ、DTDL ファイルを作成。  

といったような手順を踏んで、じっくりと考えていただきたい。  

参考として、下図にモデル化の例を示す。  

### Twin Class 候補のリストアップ  

![twin class candidates](images/twinclasscandidates.svg)  

考察  
- 製造会社は候補か？ - 製造会社が自社製品の搬送のみを考えているなら、Twin Class として扱わなくてよいだろう。一方で、複数の製造会社に導入してほしいシステムを開発するなら、Twin Classとして扱ったほうがいいだろう  - 本Tutorial では、簡単のため前者を採用
- 工場は複数あるのか？ - 複数あるなら顧客の注文発生時にどうやって生産する工場を選択する？ - 本Tutorialでは簡単のため工場は一つとして扱う。複数ある場合は、きっと選択するための基準となる Property を持つことだろう  
- 顧客は確実に Twin Class になりそうだが、同時に複数の製品を注文するのか？また、それぞれの注文の配送先は全て同じなのか、それとも複数あるのか？ - 配送先が複数なら、それを識別するための、例えば、"顧客配送先"という名前の Twin Class が必要になるだろう。 - 本 Tutorial では簡単のため、配送先は一つとし、発注は同時に複数とする。  

等々、色々と考えて、Twin Class、Relationship をリストアップしていく。  

ここでは、以下の様に Twin Class を定義する。  

|Twin Class Name|Description|
|-|-|
|Factory|製品製造場所、製品発送の起点として|
|Product|温度管理・配送対象|
|Customer|顧客。届け先は一か所とする|
|Order|複数の発注が同時に存在するので区別のため|
|Station|配送ステーション|
|Cooling Container Truck|工場から配送ステーションまで温度管理されたコンテナーで製品を輸送するトラック。本トラックにはGPSで位置情報を取得し、IoT Hub に送信する機能を持っている|
|Delivery Truck|配送ステーションから顧客に製品を保温ボックスに入れて配送するトラック。ドライバーが持っているモバイル端末が、GPSから取得した位置情報を IoT Hub に送信する。|
|Temperature Measurement Device|保冷ボックスに入れて製品の温度を測る。シナリオによれば、本機器はドライバーが持っているモバイル端末を通じて温度情報を IoT Hub に送信することになっているが、システム上では本機器からのデータとして扱う。|


---
### Twin クラスの Property 抽出

---
### Relationship のリストアップ  
Twin Class をひな型に存在する、Twin（Instance）と、別の Twin Class の Twin（Instance）の間の関係を考察する。  
開発するシステムとして意味のある関係を抽出していく。何か関係があると考えられる場合、<b><u>双方から見た相手の意味</u></b>と、<b><u>双方から見て相手が幾つあるか(多重度)</u></b>を、考察する。  


例えば…
|Source|Target|意味、多重度等|メモ|
|-|-|-|-|
|Customer|Order|1 - has_order - *|顧客は複数の注文を同時に行えるのでこの多重度にした。顧客から見た注文の多重度が 0 からなので注文をしていない顧客も Twin Graph で管理可能になっている|
|Factory|Order|0..1 - create_product_for - *|注文に対して製品を生産する工場を示す。工場側の多重度が 0..1 で 0 を許しているので、どこで生産するか決まっていない注文を Twin Graph で表現可能にしている|
|Order|Product|1 - produced_for - 0..1|注文に対して生産された製品|
|Cooling Container Truck|Product|0..1 - carry - *|温度管理されたコンテナーを持つトラックが製品を運んでいることを示す|
|Cooling Container Truck|Station|* - drive_to - 0..1|温度管理されたコンテナーを持つトラックが集配ステーションに向かって移動中であることを示す|
|…|…|…|…|

…と、いった様に、シナリオ上で発生しうる状況を元に Relationship をリストアップする。  

---
### Twin Model の定義  
これまでの考察から、いきなり JSON で DTDL でテキスト化してもいいのだが、なかなかに見通しが悪いので、UML 表記で図示化するとよい。  
![twin class relationship](images/twin_class_relationship_model.svg)  
※ あくまでも参考であり、必要最低限の Property、Relationship しか記載していない。  
※ 全ての Twin は、この図上の Twin Class のインスタンスであり、その Twin Class に定義された Property を持ち、他のTwin Class のインスタンスとの Relationship を満たさなければならない。  

---
### DTDL による Twin モデルの定義  
図で示したモデル上の Twin Class 毎に DTDL による JSON ファイルを作成する。  
DTDL の name プロパティは、日本語や空文字は使えないので英語化、"_" 化等で置き換える。  
※ 日本語表記を入れたい場合は、"displayName" に記載すればよい。  
※ プロパティで、右側に &lt;&lt;T&gt;&gt; が付与されているものは、"Telemetry" として定義する。  
Twin Class と Relationship は、以下の形式の id を必要とするので、予め <b><i>namespace</i></b>を決めておく。  

dtmi:<b><i>namespace</i></b>:<b><i>twin_class_or_relationship_name</i></b>;1

※ このチュートリアルでは、namespace を "embeddedgeorge:transport:const_temperature:sample" として話を進める。  
なお、DTDL では、Relationship の定義は、Twin Class の定義内で行うことになっているので、図に示したモデル上の Relationship は、意識的に書いている場所を決めている。各 Relationship を定義する場合は、書かれている反対側の Twin Class に定義を行う。  

DTDL で定義したファイルをサンプルとして示す。  
|Twin Class|DTDL FIle|
|-|-|
|Customer|[/models/Customer.json](../models/Customer.js)|
|Order|[/models/Order.json](../models/Order.js)|
|Product|[/models/Product.json](../models/Product.js)|
|Factory|[/models/Factory.json](../models/Factory.js)|
|Station|[/models/Station.json](../models/Station.js)|
|Temperature Measurement Device|[/models/TemperatureMeasurementDevice.json](../models/TemperatureMeasurementDevice.js)|
|Truck|[/models/Truck.json](../models/Truck.js)|
|Delivery Truck|[/models/DeliveryTruck.json](../models/DeliveryTruck.js)|
|Cooling Container Truck|[/models/CoolingContainerTruck.json](../models/CoolingContainerTruck.js)|


#include "map.h"
#include <QGraphicsScene>
#include <QGraphicsRectItem>
#include <QPainter>


Map::Map() : QObject(), QGraphicsRectItem()
{
    QPainter Filler;
    QBrush Fill(Qt::black,Qt::SolidPattern);
    QBrush Hill(Qt::black,Qt::SolidPattern);
    QBrush Dill(Qt::black,Qt::SolidPattern);
    QBrush Mill(Qt::black,Qt::SolidPattern);
    QBrush Lill(Qt::black,Qt::SolidPattern);

    D1 = new QGraphicsRectItem(0,0,20,100);
    D1->setBrush(Fill);

    D2 = new QGraphicsRectItem(20,0,430,40);
    D2->setBrush(Fill);

    D3 = new QGraphicsRectItem(450,0,350,40);
    D3->setBrush(Fill);

    D4 = new QGraphicsRectItem(40,100,30,100);
    D4->setBrush(Fill);

    D5 = new QGraphicsRectItem(100,100,270,80);
    D5->setBrush(Fill);

    D6 = new QGraphicsRectItem(390,100,60,80);
    D6->setBrush(Fill);

    D7 = new QGraphicsRectItem(470,100,60,80);
    D7->setBrush(Hill);

    D8 = new QGraphicsRectItem(550,120,210,40);
    D8->setBrush(Hill);

    D9 = new QGraphicsRectItem(780,80,20,500);
    D9->setBrush(Hill);

    D10 = new QGraphicsRectItem(0,150,20,280);
    D10->setBrush(Hill);

    D11 = new QGraphicsRectItem(65,260,20,170);
    D11->setBrush(Hill);

    D12 = new QGraphicsRectItem(120,210,50,120);
    D12->setBrush(Hill);

    D13 = new QGraphicsRectItem(200,240,90,90);
    D13->setBrush(Dill);

    D14 = new QGraphicsRectItem(320,210,55,75);
    D14->setBrush(Dill);

    D15 = new QGraphicsRectItem(410,210,55,75);
    D15->setBrush(Dill);

    D16 = new QGraphicsRectItem(490,210,60,260);
    D16->setBrush(Dill);

    D17 = new QGraphicsRectItem(580,210,110,55);
    D17->setBrush(Dill);

    D18 = new QGraphicsRectItem(320,325,55,75);
    D18->setBrush(Dill);

    D19 = new QGraphicsRectItem(410,325,55,75);
    D19->setBrush(Dill);

    D20 = new QGraphicsRectItem(580,300,110,75);
    D20->setBrush(Mill);

    D21 = new QGraphicsRectItem(0,480,20,100);
    D21->setBrush(Mill);

    D22 = new QGraphicsRectItem(50,460,100,60);
    D22->setBrush(Mill);

    D23 = new QGraphicsRectItem(255,450,65,65);
    D23->setBrush(Mill);

    D24 = new QGraphicsRectItem(390,450,55,55);
    D24->setBrush(Mill);

    D25 = new QGraphicsRectItem(580,480,40,40);
    D25->setBrush(Mill);

    D26 = new QGraphicsRectItem(0,580,800,20);
    D26->setBrush(Lill);


}

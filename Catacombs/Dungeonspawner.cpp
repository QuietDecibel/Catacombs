#include <QGraphicsTextItem>
#include "Dungeonspawner.h"
#include "PlaySquare.h"
#include "items.h"
#include "monster.h"
#include "map.h"
#include <QTimer>
#include "startwindow.h"



DungeonSpawner::DungeonSpawner(QWidget *parent)
{
    //Creates Scene or All Game Data
    theWorld = new QGraphicsScene();
    theWorld->setSceneRect(0,0,800,600);

    //Create a View to see the scene
    Level = new QGraphicsView();
    Level->setHorizontalScrollBarPolicy(Qt::ScrollBarAlwaysOff);
    Level->setVerticalScrollBarPolicy(Qt::ScrollBarAlwaysOff);
    Level->setRenderHint(QPainter::Antialiasing);
    Level->setScene(theWorld);
    Level->show();
    Level->setFixedSize(800,600);
    //Applies Color to and shading
    QPainter BG;
    QBrush Fill(Qt::lightGray,Qt::Dense5Pattern);
    theWorld->setBackgroundBrush(Fill);

    //Creates an Item(player), Define its shape, Adds Player to scene
    playR = new PlaySquare();
    playR->FullSet.getArms();
    playR->setRect(0,0,10,10); // The 10 by 10 changes the default size.
    theWorld->addItem(playR);

    //Makes the item(player) Focusable, then sets position to center of screen.
    playR->setFlag(QGraphicsItem::ItemIsFocusable);
    playR->setFocus();
    playR->setPos(Level->width()/2 - 10,Level->height()/2 - playR->rect().height());

    //Creates item(Items) in the scene
    //********************************Tried Both a for loop and a Recursive Function, but neither worked.******************************//
    Items * items[12];
    items[0] = new Items();
    LevelItems.push_back(items[0]);
    theWorld->addItem(items[0]);

    items[1] = new Items();
    LevelItems.push_back(items[1]);
    theWorld->addItem(items[1]);

    items[2] = new Items();
    LevelItems.push_back(items[2]);
    theWorld->addItem(items[2]);

    items[3] = new Items();
    LevelItems.push_back(items[3]);
    theWorld->addItem(items[3]);

    items[4] = new Items();
    LevelItems.push_back(items[4]);
    theWorld->addItem(items[4]);

    items[5] = new Items();
    LevelItems.push_back(items[5]);
    theWorld->addItem(items[5]);

    items[6] = new Items();
    LevelItems.push_back(items[6]);
    theWorld->addItem(items[6]);

    items[7] = new Items();
    LevelItems.push_back(items[7]);
    theWorld->addItem(items[7]);

    items[8] = new Items();
    LevelItems.push_back(items[8]);
    theWorld->addItem(items[8]);

    items[9] = new Items();
    LevelItems.push_back(items[9]);
    theWorld->addItem(items[9]);

    items[10] = new Items();
    LevelItems.push_back(items[10]);
    theWorld->addItem(items[10]);

    items[11] = new Items();
    LevelItems.push_back(items[11]);
    theWorld->addItem(items[11]);

    //Creates a Map, puts it into the scene
    Walls = new Map();
    theWorld->addItem(Walls);
    theWorld->addItem(Walls->D1);
    theWorld->addItem(Walls->D2);
    theWorld->addItem(Walls->D3);
    theWorld->addItem(Walls->D4);
    theWorld->addItem(Walls->D5);
    theWorld->addItem(Walls->D6);
    theWorld->addItem(Walls->D7);
    theWorld->addItem(Walls->D8);
    theWorld->addItem(Walls->D9);
    theWorld->addItem(Walls->D10);
    theWorld->addItem(Walls->D11);
    theWorld->addItem(Walls->D12);
    theWorld->addItem(Walls->D13);
    theWorld->addItem(Walls->D14);
    theWorld->addItem(Walls->D15);
    theWorld->addItem(Walls->D16);
    theWorld->addItem(Walls->D17);
    theWorld->addItem(Walls->D18);
    theWorld->addItem(Walls->D19);
    theWorld->addItem(Walls->D20);
    theWorld->addItem(Walls->D21);
    theWorld->addItem(Walls->D22);
    theWorld->addItem(Walls->D23);
    theWorld->addItem(Walls->D24);
    theWorld->addItem(Walls->D25);
    theWorld->addItem(Walls->D26);

    //Creates the Gamebar and Initialize all the player Stats ******Test TODO: Link Character Creator results to here.
    gameBar = new GameBar;
    theWorld->addItem(gameBar);
    theWorld->addItem(gameBar->Heal);
    theWorld->addItem(gameBar->Magi);

    //Spawns Monsters
    QTimer *Spawner = new QTimer();
    QObject::connect(Spawner,SIGNAL(timeout()),playR,SLOT(Incoming()));
    Spawner->start(2000);

}

void DungeonSpawner::Item()
{
    Items *item = new Items();
    scene()->addItem(item);
    LevelItems.push_back(item);
}


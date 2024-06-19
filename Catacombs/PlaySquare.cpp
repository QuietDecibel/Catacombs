#include <QGraphicsScene>
#include "PlaySquare.h"
#include "Dungeonspawner.h"
#include <QKeyEvent>
#include "Weapon.h"
#include "monster.h"
#include "items.h"
#include <QTimer>
#include "map.h"
#include <QGraphicsTextItem>

#include <QtDebug>
//extern DungeonSpawner *DMSGame;
extern GameBar *gamBar;
extern QGraphicsScene *theWorld;


QString PlaySquare::getName()
{
    return Name;
}

void PlaySquare::setName( QString value)
{
    Name = value;
}

QString PlaySquare::getJobClass()
{
    return JobClass;
}

void PlaySquare::setJobClass( QString value)
{
    JobClass = value;
}

int PlaySquare::getHealth() const
{
    return Health;
}

void PlaySquare::setHealth(int value)
{
    Health = value;
}

int PlaySquare::getMagic() const
{
    return Magic;
}

void PlaySquare::setMagic(int value)
{
    Magic = value;
}

int PlaySquare::getAttack() const
{
    return Attack;
}

void PlaySquare::setAttack(int value)
{
    Attack = value;
}

int PlaySquare::getDefense() const
{
    return Defense;
}

void PlaySquare::setDefense(int value)
{
    Defense = value;
}

double PlaySquare::getCritical() const
{
    return Critical;
}

void PlaySquare::setCritical(double value)
{
    Critical = value;
}

int PlaySquare::getFace()
{
    return Face;
}

void PlaySquare::setFace(int value)
{
    Face = value;
}

PlaySquare::PlaySquare()
{
    //Applies Color to and shading
    QPainter Filler;
    QPen Outter(Qt::blue);
    QBrush Fill(Qt::gray,Qt::DiagCrossPattern);
    this->setBrush(Fill);
    this->setPen(Outter);

    setName("Unknown");
    setJobClass("Vagrant");
    setFace(1);
    setHealth(0);
    setMagic(0);
    setAttack(0);
    setDefense(0);
    setCritical(0);

    FullSet.setHead(0);
    FullSet.setChest(0);
    FullSet.setArms(0);
    FullSet.setLegs(0);
    FullSet.setHands(0);
    FullSet.setFeet(0);
}

void PlaySquare::keyPressEvent(QKeyEvent *event)
{

    switch(event->key()){
        case Qt::Key_Left:
            if(pos().x() > 0){
                setPos(x()-5,y());
                this->setFace(3);
                qDebug() << getFace() << "West";
            };
            break;
        case Qt::Key_Right:
            if(pos().x() + 10 < 795){
                setPos(x()+5,y());
                this->setFace(1);
                qDebug() << getFace()<< "East";
            };
            break;
        case Qt::Key_Up:
           if(pos().y() > 0){
                setPos(x(),y()-5);
                this->setFace(0);
                qDebug() << getFace()<<"North";
            };
            break;
        case Qt::Key_Down:
            if((pos().y() + 15) < 600 ){
                setPos(x(),y()+5);
                this->setFace(2);
                qDebug() << getFace()<< "South";
            };
            break;
        case Qt::Key_Space:
          //Create the Weapon
          Weapon *Arrow = new Weapon(Face);
          scene()->addItem(Arrow);
          Arrow->setPos(x()+5,y());
          qDebug() << Arrow->pos()<< "Made A Arrow Swoosh" << "Face: " << this->getFace();
          break;
    };
    QList<QGraphicsItem *>  TouchedItems = collidingItems();
    for(int i = 0, n = TouchedItems.size(); i < n; ++i){
        if(typeid(*TouchedItems[i]) == typeid(Monster)){
            //Makes Monster Dissappear from Screen.
            scene()->removeItem(TouchedItems[i]);
            this->Health = this->Health - 25;

            //Item Collected notification
            QGraphicsTextItem *GameOver = new QGraphicsTextItem();
            GameOver->setDefaultTextColor(Qt::darkRed);
            GameOver->setPlainText("Item Collected");
            GameOver->setFont(QFont("OldEnglish",50));
            GameOver->setTextWidth(20);
            GameOver->setPos(100,200);

            //Removes monster
            delete TouchedItems[i];
            delete this;
            delete GameOver;
            qDebug() << "Player Got too Close, Monster Killed you!" << "Respawn?";
            //exit(0);
            return;
        };
        if(typeid(*TouchedItems[i]) == typeid(Items)){
            //Makes Item Dissappear from Screen.
            scene()->removeItem(TouchedItems[i]);

            //Item Collected notification
            QGraphicsTextItem *Collect = new QGraphicsTextItem();
            Collect->setDefaultTextColor(Qt::yellow);
            Collect->setPlainText("Item Collected");
            Collect->setFont(QFont("OldEnglish",20));
            Collect->setTextWidth(20);
            Collect->setPos(200,400);

            //Removes Item
            delete TouchedItems[i];
            delete Collect;
            qDebug() << "Item Absorbed";
            return;
        };
    };


}

void PlaySquare::Incoming()
{
   //Creates Enemys for Testing purposes.
    if(MonsterTotal < 20){
        Monster * Enemy = new Monster();
        scene()->addItem(Enemy);
        MonsterTotal++;
        return;
    }
    return;

}

int PlaySquare::Armor::getHead() const
{
    return Head;
}

void PlaySquare::Armor::setHead(int value)
{
    Head = value;
}

int PlaySquare::Armor::getChest() const
{
    return Chest;
}

void PlaySquare::Armor::setChest(int value)
{
    Chest = value;
}

int PlaySquare::Armor::getArms() const
{
    return Arms;
}

void PlaySquare::Armor::setArms(int value)
{
    Arms = value;
}

int PlaySquare::Armor::getHands() const
{
    return Hands;
}

void PlaySquare::Armor::setHands(int value)
{
    Hands = value;
}

int PlaySquare::Armor::getLegs() const
{
    return Legs;
}

void PlaySquare::Armor::setLegs(int value)
{
    Legs = value;
}

int PlaySquare::Armor::getFeet() const
{
    return Feet;
}

void PlaySquare::Armor::setFeet(int value)
{
    Feet = value;
}

void PlaySquare::DWallHit()
{
    if(scene()->collidingItems(this).isEmpty()){
        setPos(x(),y());
    };
    return;
}

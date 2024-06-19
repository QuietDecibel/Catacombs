#include "Weapon.h"
#include <QTimer>
#include <QDebug>
#include <QList>
#include <QGraphicsScene>
#include "Dungeonspawner.h"
#include "PlaySquare.h"
#include "monster.h"
#include "items.h"



int Weapon::getAim() const
{
    return Aim;
}

void Weapon::setAim(int value)
{
    Aim = value;
}

Weapon::Weapon(int aim)
{
    //Weapons Shape size color
    setRect(0,0,2,15);
    setAim(aim);

    QPainter Filler;
    QPen Outter(Qt::magenta);
    QBrush Fill(Qt::magenta,Qt::SolidPattern);
    this->setBrush(Fill);
    this->setPen(Outter);

    //Connecting the timer with the weapons public slot move()
    QTimer * timer = new QTimer();
    connect(timer,SIGNAL(timeout()),this,SLOT(move()));
    timer->start(50);
}

void Weapon::move()
{
    //Arrow Kills Enemy
    QList<QGraphicsItem *>  HitEntities = collidingItems();
    for(int i = 0, n = HitEntities.size(); i < n; ++i){
        if(typeid(*HitEntities[i]) == typeid(Monster)){
            //Makes Monster Dissappear from Screen.
            scene()->removeItem(HitEntities[i]);
            scene()->removeItem(this);
            //Create Item Object and add it to the scene from monster death.

            //Removes monster and arrow data
            delete HitEntities[i];
            delete this;
            return;
        };
    };

    //Spawn Point, Directional Movement and Deletion.
    qDebug() << "Weapon Move";

    switch(Aim){
        case 0:
            this->setPos(x(),y()-5);
            if(this->pos().y() < -15 || this->pos().x() < -15){
                scene()->removeItem(this);
                delete this;
                qDebug() << "Moved North - Deleted Arrow";
            };
            break;
        case 1:
            this->setPos(x()+5,y());
            if(this->pos().y() > 600 || this->pos().x() > 800){
                scene()->removeItem(this);
                delete this;
                qDebug() << "Moved East - Deleted Arrow";
            };
            break;
        case 2:
            this->setPos(x(),y()+5);
            if(this->pos().y() > 600 || this->pos().x() > 800){
                scene()->removeItem(this);
                delete this;
                qDebug() << "Moved South - Deleted Arrow";
            };
            break;
        case 3:
            this->setPos(x()-5,y());
            if(this->pos().y() < -15 || this->pos().x() < -15){
                scene()->removeItem(this);
                delete this;
                qDebug() << "Moved West - Deleted Arrow";
            };
            break;
        default:
            this->setPos(x()-5,y());
            if(this->pos().y() < -15 || this->pos().x() < -15){
                scene()->removeItem(this);
                delete this;
                qDebug() << "Default - Deleted Arrow";
            };

    };
};



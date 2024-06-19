#ifndef WEAPON_H
#define WEAPON_H

#include <QGraphicsRectItem>
#include <QObject>
#include "PlaySquare.h"

class Weapon:public QObject,public QGraphicsRectItem{
    Q_OBJECT

private:
    int Aim;
public:
    Weapon(int aim);
    int getAim() const;
    void setAim(int value);

public slots:
    void move();

};


#endif // WEAPON_H

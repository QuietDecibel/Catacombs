#ifndef QUESTIONS_H
#define QUESTIONS_H
#include <QString>

class Questions{
private:
    QString Q1;
    QString A1;
    QString A2;
    QString A3;
    QString A4;

public:

    QString getQ1() const;
    void setQ1(const QString value);
    QString getA1() const;
    void setA1(const QString value);
    QString getA2() const;
    void setA2(const QString value);
    QString getA3() const;
    void setA3(const QString value);
    QString getA4() const;
    void setA4(const QString value);
};

inline QString Questions::getA1() const
{
    return A1;
}

inline void Questions::setA1(const QString value)
{
    A1 = value;
}

inline QString Questions::getA2() const
{
    return A2;
}

inline void Questions::setA2(const QString value)
{
    A2 = value;
}

inline QString Questions::getA3() const
{
    return A3;
}

inline void Questions::setA3(const QString value)
{
    A3 = value;
}

inline QString Questions::getA4() const
{
    return A4;
}

inline void Questions::setA4(const QString value)
{
    A4 = value;
}

inline QString Questions::getQ1() const
{
    return Q1;
}

inline void Questions::setQ1(const QString value)
{
    Q1 = value;
}




#endif // QUESTIONS_H
